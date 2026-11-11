#include "kawarimi.h"
#include "game.h"
#include "hook.h"
#include <algorithm>
#include <intrin.h>
#include <mutex>

namespace kw {

namespace {

struct CriFileInfo {
    void* fs;
    const char* path;
    int64_t offset, readSize, extractSize;
    uint32_t binderId, reserved;
};

using FindFn = int (*)(void*, const char*, CriFileInfo*, int*);
using LoadFn = int (*)(void*, void*, const char*, int64_t, int64_t, void*, int64_t);
using SlotFn = int (*)(void*);

decltype(&CreateFileW) g_origCreateFileW = CreateFileW;
decltype(&CreateFileA) g_origCreateFileA = CreateFileA;
FindFn g_origFind = nullptr;
LoadFn g_origLoad = nullptr;
SlotFn g_origSlotStatus = nullptr, g_origSlotDone = nullptr;
bool g_logPaths = false;
std::mutex g_doneMu;
std::vector<void*> g_done;

std::wstring Lower(std::wstring s) {
    for (auto& c : s) c = (wchar_t)towlower(c);
    return s;
}

bool ServedBy(const Skin& k, const std::string& prefix, const std::wstring& file, const std::wstring& lfile, std::wstring& out) {
    std::wstring code = Lower(std::wstring(prefix.begin(), prefix.end()));
    if (code.empty() || lfile.compare(0, code.size(), code) != 0) return false;
    std::wstring cand = std::wstring(k.dir.begin(), k.dir.end()) + L"\\" + file;
    if (GetFileAttributesW(cand.c_str()) == INVALID_FILE_ATTRIBUTES) return false;
    out = cand;
    return true;
}

bool ModelFile(const std::wstring& lfile, size_t prefix) {
    return lfile.compare(prefix, 3, L"bod") == 0 || lfile.compare(prefix, 3, L"acc") == 0;
}

template <class F>
bool AnySkin(const Snapshot* s, F f) {
    for (auto& e : s->byCode)
        for (auto& k : e.second)
            if (f(k)) return true;
    for (auto& e : s->byId)
        for (auto& k : e.second)
            if (f(k)) return true;
    return false;
}

void MarkDone(void* loader, bool done) {
    std::lock_guard<std::mutex> lk(g_doneMu);
    auto it = std::find(g_done.begin(), g_done.end(), loader);
    if (done && it == g_done.end()) g_done.push_back(loader);
    if (!done && it != g_done.end()) g_done.erase(it);
}

bool IsDone(void* slot) {
    void* loader = slot ? *(void**)((uint8_t*)slot + 8) : nullptr;
    std::lock_guard<std::mutex> lk(g_doneMu);
    return loader && std::find(g_done.begin(), g_done.end(), loader) != g_done.end();
}

int Hook_SlotStatus(void* slot) {
    return IsDone(slot) ? 2 : g_origSlotStatus(slot);
}
int Hook_SlotDone(void* slot) {
    return IsDone(slot) ? 1 : g_origSlotDone(slot);
}

bool VirtualPath(const char* path, std::wstring& out) {
    if (!path || !*path || !g_snap.load()) return false;
    return Redirect(std::wstring(path, path + strlen(path)), out);
}

void LogPath(const char* path) {
    static std::mutex mu;
    static std::map<std::string, int> seen;
    std::lock_guard<std::mutex> lk(mu);
    if (seen.size() < 20000 && seen[path]++ == 0) Log("VFS find %s", path);
}

int Hook_Find(void* binder, const char* path, CriFileInfo* fi, int* exist) {
    if (g_logPaths && path) LogPath(path);
    if (path) NoteModelRequest(path);
    std::wstring file;
    WIN32_FILE_ATTRIBUTE_DATA a;
    if (VirtualPath(path, file) && GetFileAttributesExW(file.c_str(), GetFileExInfoStandard, &a)) {
        int64_t size = (int64_t)a.nFileSizeHigh << 32 | a.nFileSizeLow;
        if (exist) *exist = 1;
        if (fi) {
            memset(fi, 0, sizeof *fi);
            fi->path = path;
            fi->readSize = size;
            fi->extractSize = size;
            fi->binderId = game::kVirtualBinderId;
        }
        return 0;
    }
    return g_origFind(binder, path, fi, exist);
}

bool CopyFileInto(const std::wstring& file, int64_t offset, int64_t size, void* buf, int64_t bufSize, int64_t& want) {
    want = -1;
    HANDLE h = g_origCreateFileW(file.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr);
    if (h == INVALID_HANDLE_VALUE) return false;
    LARGE_INTEGER len{}, pos{};
    GetFileSizeEx(h, &len);
    pos.QuadPart = offset > 0 ? offset : 0;
    want = len.QuadPart - pos.QuadPart;
    if (size >= 0 && size < want) want = size;
    if (bufSize >= 0 && bufSize < want) want = bufSize;
    DWORD got = 0;
    bool ok = want >= 0 && SetFilePointerEx(h, pos, nullptr, FILE_BEGIN) && ReadFile(h, buf, (DWORD)want, &got, nullptr) && got == (DWORD)want;
    CloseHandle(h);
    return ok;
}

int Hook_Load(void* loader, void* binder, const char* path, int64_t offset, int64_t size, void* buf, int64_t bufSize) {
    bool fromSlot = (uintptr_t)_ReturnAddress() == g_base + game::kSlotLoadReturn;
    std::wstring file;
    if (fromSlot && loader && buf && VirtualPath(path, file)) {
        int64_t want;
        bool ok = CopyFileInto(file, offset, size, buf, bufSize, want);
        Log("[%llu] VFS load %s <- %ls (%lld bytes, %s)", (unsigned long long)GetTickCount64(), path, file.c_str(), (long long)want,
            ok ? "ok" : "FAILED, game file used");
        if (ok) {
            MarkDone(loader, true);
            return 0;
        }
    }
    MarkDone(loader, false);
    return g_origLoad(loader, binder, path, offset, size, buf, bufSize);
}

HANDLE WINAPI Hook_CreateFileW(LPCWSTR n, DWORD a, DWORD sh, LPSECURITY_ATTRIBUTES sa, DWORD cd, DWORD fl, HANDLE t) {
    std::wstring r;
    if (n && Redirect(n, r)) {
        Log("redirect %ls -> %ls", n, r.c_str());
        n = r.c_str();
    }
    return g_origCreateFileW(n, a, sh, sa, cd, fl, t);
}

HANDLE WINAPI Hook_CreateFileA(LPCSTR n, DWORD a, DWORD sh, LPSECURITY_ATTRIBUTES sa, DWORD cd, DWORD fl, HANDLE t) {
    std::wstring r;
    if (n && Redirect(std::wstring(n, n + strlen(n)), r)) {
        Log("redirect %s -> %ls", n, r.c_str());
        return g_origCreateFileW(r.c_str(), a, sh, sa, cd, fl, t);
    }
    return g_origCreateFileA(n, a, sh, sa, cd, fl, t);
}

}

bool Redirect(const std::wstring& path, std::wstring& out) {
    const Snapshot* s = g_active ? g_snap.load() : nullptr;
    if (!s) return false;
    std::wstring p = path;
    for (auto& c : p)
        if (c == L'/') c = L'\\';
    std::wstring lower = Lower(p);
    size_t pos = lower.rfind(L"data\\spc\\");
    if (pos == std::wstring::npos) return false;
    std::wstring file = p.substr(pos + 9), lfile = lower.substr(pos + 9);
    if (NewCodesAllowed() && AnySkin(s, [&](const Skin& k) { return ServedBy(k, std::string(k.code, strnlen(k.code, 8)), file, lfile, out); })) return true;
    bool visual = VisualOnly();
    return AnySkin(s, [&](const Skin& k) {
        return !k.replaces.empty() && (!visual || ModelFile(lfile, k.replaces.size())) &&
               ActiveSkinFor(k.replaces) == std::string(k.code, strnlen(k.code, 8)) && ServedBy(k, k.replaces, file, lfile, out);
    });
}

void NoteModelRequest(const char* path) {
    const char* p = strstr(path, "data/spc/");
    if (!p) return;
    p += 9;
    const char* bod = strstr(p, "bod1.xfbin");
    if (!bod || bod[10] != 0 || bod - p < 3 || bod - p > 7 || strchr(p, '/')) return;
    NoteCostume(std::string(p, bod - p));
}

void InstallFileHooks(HMODULE exe, bool logPaths) {
    g_logPaths = logPaths;
    if (!Skip("vfs")) {
        uint8_t* b = (uint8_t*)g_base;
        bool f = !Skip("vf1") && InstallHook(b + game::kCriFind, game::kCriFindPrologue, sizeof game::kCriFindPrologue, (void*)Hook_Find, (void**)&g_origFind);
        bool l = !Skip("vf2") && InstallHook(b + game::kCriLoad, game::kCriLoadPrologue, sizeof game::kCriLoadPrologue, (void*)Hook_Load, (void**)&g_origLoad);
        bool s = !Skip("vf3") &&
                 InstallHook(b + game::kSlotStatus, game::kSlotStatusPrologue, sizeof game::kSlotStatusPrologue, (void*)Hook_SlotStatus,
                             (void**)&g_origSlotStatus) &&
                 InstallHook(b + game::kSlotDone, game::kSlotDonePrologue, sizeof game::kSlotDonePrologue, (void*)Hook_SlotDone, (void**)&g_origSlotDone);
        Log("virtual file hooks: find=%d load=%d status=%d%s", f, l, s, logPaths ? " (path log on)" : "");
    }
    int w = 0, a = 0;
    if (!Skip("iat")) {
        w = PatchIat(exe, (void*)CreateFileW, (void*)Hook_CreateFileW);
        a = PatchIat(exe, (void*)CreateFileA, (void*)Hook_CreateFileA);
    }
    Log("file redirect IAT slots: W=%d A=%d", w, a);
}

}
