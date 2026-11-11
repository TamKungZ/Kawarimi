#include "kawarimi.h"
#include "game.h"
#include "hook.h"
#include <algorithm>
#include <cstdio>
#include <mutex>

namespace kw {

namespace {

struct CapWatch {
    HANDLE h;
    uintptr_t buf;
    DWORD n;
    ULONGLONG t, ext;
};

std::mutex g_capMu;
std::vector<CapWatch> g_capList;
std::wstring g_capDir;
decltype(&ReadFile) g_origReadFile = ReadFile;

BOOL WINAPI Hook_ReadFile(HANDLE h, LPVOID buf, DWORD n, LPDWORD got, LPOVERLAPPED ov) {
    BOOL r = g_origReadFile(h, buf, n, got, ov);
    DWORD err = r ? 0 : GetLastError();
    if (buf && (r || err == ERROR_IO_PENDING)) {
        DWORD g = (r && got) ? *got : n;
        ULONGLONG now = GetTickCount64();
        std::lock_guard<std::mutex> lk(g_capMu);
        bool ext = false;
        for (auto& w : g_capList)
            if (w.h == h && (uintptr_t)buf == w.buf + w.n) {
                w.n += g;
                w.ext = now;
                ext = true;
                break;
            }
        bool candidate = n >= 256 && g_capList.size() < 6000 && (!r || memcmp(buf, game::kEncryptedSignature, 8) == 0);
        if (!ext && candidate) {
            for (size_t i = 0; i < g_capList.size(); i++)
                if (g_capList[i].buf == (uintptr_t)buf) {
                    g_capList.erase(g_capList.begin() + i);
                    break;
                }
            g_capList.push_back({h, (uintptr_t)buf, g, now, now});
        }
    }
    if (!r) SetLastError(err);
    return r;
}

uint32_t Be32(const uint8_t* p) {
    return (uint32_t)p[0] << 24 | (uint32_t)p[1] << 16 | (uint32_t)p[2] << 8 | p[3];
}

uint64_t WalkChunks(uintptr_t buf, uint64_t start, uint32_t ver, uint32_t mc) {
    uint64_t p = start;
    int chunks = 0;
    for (;;) {
        uint8_t c[12];
        if (!Rd((void*)(buf + p), c, 12)) break;
        uint32_t size = Be32(c), idx = Be32(c + 4);
        uint16_t v = (uint16_t)(c[8] << 8 | c[9]);
        if (v != (uint16_t)ver || idx >= (mc ? mc : 1) + 64 || size > (128u << 20)) break;
        uint8_t probe;
        if (size && !Rd((void*)(buf + p + 12 + size - 1), &probe, 1)) break;
        p += 12 + size;
        chunks++;
    }
    return chunks > 0 ? p : 0;
}

uint64_t MeasureByLength(uintptr_t buf, uint64_t hint, uint32_t ms, uint32_t mc, uint32_t extra) {
    if (hint < 64 || hint >= (64u << 20)) return 0;
    std::vector<uint8_t> all((size_t)hint);
    if (!Rd((void*)buf, all.data(), (size_t)hint)) return 0;
    for (uint64_t start = 40ull + ms; start < 40ull + ms + 0x10000 && start + 12 <= hint; start += 4) {
        uint64_t p = start;
        int k = 0;
        while (p + 12 <= hint) {
            uint32_t size = Be32(&all[p]), idx = Be32(&all[p + 4]);
            if (idx >= mc + 64 || size > hint - p - 12) break;
            p += 12 + size;
            k++;
        }
        if (k >= 2 && p == hint) {
            static int shown = 0;
            if (shown++ < 30)
                Log("capture: chunk start = 40 + ms(%u) + %llu  (extra=%u, chunks=%d, size=%llu)", ms, (unsigned long long)(start - 40 - ms), extra, k,
                    (unsigned long long)hint);
            return p;
        }
    }
    return 0;
}

uint64_t MeasureNucc(uintptr_t buf, uint64_t hint) {
    uint8_t h[72];
    if (!Rd((void*)buf, h, 72)) return 0;
    uint32_t ver = Be32(h + 4), ms = Be32(h + 16), mc = std::max(Be32(h + 52), Be32(h + 60)), extra = Be32(h + 64);
    if (ver < 0x60 || ver > 0x90 || ms > 0x100000) return 0;
    uint64_t starts[5] = {28ull + ms + 8ull * extra, 40ull + ms + 8ull * extra, 40ull + ms, 28ull + ms, 40ull + ms + 4ull * extra};
    for (uint64_t start : starts)
        if (uint64_t len = WalkChunks(buf, start, ver, mc)) return len;
    return MeasureByLength(buf, hint, ms, mc, extra);
}

void WriteFileAt(const std::wstring& path, const void* data, size_t n) {
    FILE* f = nullptr;
    if (!_wfopen_s(&f, path.c_str(), L"wb") && f) {
        fwrite(data, 1, n, f);
        fclose(f);
    }
}

void ReportUnmeasurable(const CapWatch& w) {
    static int shown = 0;
    if (shown < 6 && w.n < (40u << 20)) {
        std::vector<uint8_t> raw(w.n);
        wchar_t nm[64];
        swprintf_s(nm, L"\\raw_%u_%u.bin", shown, w.n);
        if (Rd((void*)w.buf, raw.data(), w.n)) WriteFileAt(g_capDir + nm, raw.data(), raw.size());
    }
    if (shown++ >= 25) return;
    uint8_t hd[48] = {0};
    Rd((void*)w.buf, hd, 48);
    char hx[100];
    for (int i = 0; i < 48; i++) sprintf_s(hx + i * 2, 3, "%02x", hd[i]);
    uint32_t ms = 0;
    uint8_t b4[4];
    if (Rd((void*)(w.buf + 16), b4, 4)) ms = Be32(b4);
    char c1[40] = "", c2[40] = "";
    uint8_t cc[12];
    if (Rd((void*)(w.buf + 40 + ms), cc, 12))
        for (int i = 0; i < 12; i++) sprintf_s(c1 + i * 2, 3, "%02x", cc[i]);
    if (Rd((void*)(w.buf + 28 + ms), cc, 12))
        for (int i = 0; i < 12; i++) sprintf_s(c2 + i * 2, 3, "%02x", cc[i]);
    Log("capture: unmeasurable buf=%llx read=%u ms=%u chunk@40+ms=%s chunk@28+ms=%s", (unsigned long long)w.buf, w.n, ms, c1, c2);
}

DWORD WINAPI CaptureThread(LPVOID) {
    int files = 0;
    for (;;) {
        Sleep(8);
        std::vector<CapWatch> work, keep;
        {
            std::lock_guard<std::mutex> lk(g_capMu);
            work.swap(g_capList);
        }
        ULONGLONG now = GetTickCount64();
        for (auto& w : work) {
            uint8_t head[8];
            if (!Rd((void*)w.buf, head, 8)) continue;
            if (memcmp(head, "NUCC", 4) == 0 && now - w.ext > 200) {
                uint64_t len = MeasureNucc(w.buf, w.n);
                if (len < 64 || len > (200ull << 20)) {
                    ReportUnmeasurable(w);
                    continue;
                }
                std::vector<uint8_t> data((size_t)len);
                if (!Rd((void*)w.buf, data.data(), (size_t)len)) continue;
                wchar_t name[64];
                swprintf_s(name, L"\\%08x_%u.xfbin", (unsigned)(w.buf >> 4), (unsigned)len);
                WriteFileAt(g_capDir + name, data.data(), data.size());
                if (++files % 20 == 1) Log("capture: %d file(s) so far (last %u bytes)", files, (unsigned)len);
            } else if (memcmp(head, game::kEncryptedSignature, 8) == 0) {
                if (now - w.t < 30000) keep.push_back(w);
            } else if (now - w.t < 3000) {
                keep.push_back(w);
            }
        }
        if (keep.empty()) continue;
        std::lock_guard<std::mutex> lk(g_capMu);
        for (auto& k : keep) {
            bool dup = false;
            for (auto& w : g_capList)
                if (w.buf == k.buf && w.h == k.h) {
                    dup = true;
                    break;
                }
            if (!dup) g_capList.push_back(k);
        }
    }
}

}

void InstallCapture(HMODULE exe, const std::wstring& dir) {
    g_capDir = dir;
    CreateDirectoryW(g_capDir.c_str(), nullptr);
    int rf = PatchIat(exe, (void*)ReadFile, (void*)Hook_ReadFile);
    CreateThread(nullptr, 0, CaptureThread, nullptr, 0, nullptr);
    Log("capture mode ON (ReadFile IAT slots %d) -> %ls", rf, g_capDir.c_str());
}

}
