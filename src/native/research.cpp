#include "kawarimi.h"
#include "game.h"
#include "hook.h"
#include "table.h"
#include <intrin.h>
#include <mutex>
#include <tlhelp32.h>
#include <tuple>

namespace kw {

int g_watchChr = -1;

namespace {

using EntryLookupFn = void* (*)(void*, int, int);
EntryLookupFn g_origEntryLookup = nullptr;
std::atomic<int> g_watchHits{0};

void* Hook_EntryLookup(void* obj, int cid, int idx) {
    if (g_trace) {
        static std::mutex mu;
        static std::map<std::tuple<int, int, uint32_t, uint64_t>, int> seen;
        static int total = 0;
        uint32_t ret = (uint32_t)((uintptr_t)_ReturnAddress() - g_base);
        std::lock_guard<std::mutex> lk(mu);
        int& n = seen[{cid, idx, ret, GetTickCount64() / 2000}];
        if (n++ < 1 && total++ < 6000) Log("EntryLookup cid=%d idx=%d ret=+0x%x", cid, idx, ret);
    }
    return g_origEntryLookup(obj, cid, idx);
}

std::string ExeFrames(uintptr_t rsp) {
    std::string frames;
    uint8_t stk[0x300];
    SIZE_T got = 0;
    if (!ReadProcessMemory(GetCurrentProcess(), (void*)rsp, stk, sizeof stk, &got)) return frames;
    int shown = 0;
    for (size_t i = 0; i + 8 <= got && shown < 12; i += 8) {
        uintptr_t q = *(uintptr_t*)(stk + i);
        if (q >= g_base && q < g_base + 0x1500000) {
            char b[24];
            sprintf_s(b, "+0x%llx ", (unsigned long long)(q - g_base));
            frames += b;
            shown++;
        }
    }
    return frames;
}

LONG CALLBACK WatchVeh(PEXCEPTION_POINTERS ep) {
    if (ep->ExceptionRecord->ExceptionCode != EXCEPTION_SINGLE_STEP) return EXCEPTION_CONTINUE_SEARCH;
    DWORD64 dr6 = ep->ContextRecord->Dr6;
    if (!(dr6 & 0xF)) return EXCEPTION_CONTINUE_SEARCH;
    ep->ContextRecord->Dr6 = 0;
    uintptr_t rip = (uintptr_t)ep->ContextRecord->Rip;
    if (rip >= g_base + game::kGetSlot && rip < g_base + game::kGetSlot + 0x60) return EXCEPTION_CONTINUE_EXECUTION;
    static std::mutex mu;
    static std::map<std::tuple<uintptr_t, int, uint64_t>, int> seen;
    std::lock_guard<std::mutex> lk(mu);
    int which = (dr6 & 1) ? 0 : (dr6 & 2) ? 1 : (dr6 & 4) ? 2 : 3;
    int& n = seen[{rip - g_base, which, GetTickCount64() / 3000}];
    if (n++ >= 1 || g_watchHits.fetch_add(1) > 1500) return EXCEPTION_CONTINUE_EXECUTION;
    Log("WATCH dr%d hit rip=+0x%llx thread=%lu frames: %s", which, (unsigned long long)(rip - g_base), GetCurrentThreadId(),
        ExeFrames(ep->ContextRecord->Rsp).c_str());
    return EXCEPTION_CONTINUE_EXECUTION;
}

int ArmThreads(const uintptr_t addr[4]) {
    DWORD me = GetCurrentThreadId(), pid = GetCurrentProcessId();
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
    if (snap == INVALID_HANDLE_VALUE) return 0;
    THREADENTRY32 te;
    te.dwSize = sizeof te;
    int n = 0;
    for (BOOL ok = Thread32First(snap, &te); ok; ok = Thread32Next(snap, &te)) {
        if (te.th32OwnerProcessID != pid || te.th32ThreadID == me) continue;
        HANDLE th = OpenThread(THREAD_SUSPEND_RESUME | THREAD_GET_CONTEXT | THREAD_SET_CONTEXT, FALSE, te.th32ThreadID);
        if (!th) continue;
        if (SuspendThread(th) != (DWORD)-1) {
            CONTEXT c = {};
            c.ContextFlags = CONTEXT_DEBUG_REGISTERS;
            if (GetThreadContext(th, &c)) {
                c.Dr0 = addr[0];
                c.Dr1 = addr[1];
                c.Dr2 = addr[2];
                c.Dr3 = addr[3];
                DWORD64 dr7 = 0;
                for (int i = 0; i < 4; i++) dr7 |= (1ull << (i * 2)) | (3ull << (16 + i * 4)) | (2ull << (18 + i * 4));
                c.Dr7 = dr7;
                if (SetThreadContext(th, &c)) n++;
            }
            ResumeThread(th);
        }
        CloseHandle(th);
    }
    CloseHandle(snap);
    return n;
}

}

void InstallResearchHooks() {
    void* t = (void*)(g_base + game::kEntryLookup);
    bool ok = InstallHook(t, game::kEntryLookupPrologue, sizeof game::kEntryLookupPrologue, (void*)Hook_EntryLookup, (void**)&g_origEntryLookup);
    Log(ok ? "entry lookup hook installed" : "entry lookup hook NOT installed");
}

void ArmWatch(int chr) {
    AddVectoredExceptionHandler(1, WatchVeh);
    uintptr_t addr[4] = {0, 0, 0, 0};
    CostumeVec v;
    if (GetCostumeVec(v)) {
        int found = 0;
        for (uintptr_t p = v.begin; p < v.end && found < 3; p += game::kSelectorEntrySize) {
            int cid = 0;
            if (Rd((void*)p, &cid, 4) && cid == chr) addr[found++] = p;
        }
        addr[3] = v.obj + 0x20;
    }
    if (!addr[0]) {
        Log("WATCH: no vector entries for chr=%d", chr);
        return;
    }
    int n = ArmThreads(addr);
    Log("WATCH armed on chr=%d vector entries %llx %llx %llx end@%llx in %d thread(s)", chr, (unsigned long long)addr[0], (unsigned long long)addr[1],
        (unsigned long long)addr[2], (unsigned long long)addr[3], n);
}

}
