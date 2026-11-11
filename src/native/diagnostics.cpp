#include "kawarimi.h"

namespace kw {

namespace {

void CppTypeName(const EXCEPTION_RECORD* er, char* type, size_t cap) {
    if (er->NumberParameters < 4) return;
    uintptr_t base = er->ExceptionInformation[3], ti = er->ExceptionInformation[2];
    int rvaCta = 0, n = 0, rvaCt = 0, rvaTd = 0;
    if (Rd((void*)(ti + 12), &rvaCta, 4) && Rd((void*)(base + rvaCta), &n, 4) && n > 0 && Rd((void*)(base + rvaCta + 4), &rvaCt, 4) &&
        Rd((void*)(base + rvaCt + 4), &rvaTd, 4))
        Rd((void*)(base + rvaTd + 16), type, cap - 1);
}

std::string ModuleFrames(uintptr_t rsp) {
    std::string frames;
    uint8_t stk[0x600];
    SIZE_T got = 0;
    if (!ReadProcessMemory(GetCurrentProcess(), (void*)rsp, stk, sizeof stk, &got)) return frames;
    int shown = 0;
    for (size_t i = 0; i + 8 <= got && shown < 16; i += 8) {
        uintptr_t q = *(uintptr_t*)(stk + i);
        HMODULE hm = nullptr;
        if (q <= 0x10000 || !GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCWSTR)q, &hm) || !hm)
            continue;
        wchar_t nm[MAX_PATH];
        GetModuleFileNameW(hm, nm, MAX_PATH);
        std::wstring w = nm;
        w = w.substr(w.find_last_of(L'\\') + 1);
        if (w == L"ntdll.dll" || w == L"KERNELBASE.dll" || w == L"kernel32.dll") continue;
        char b[160];
        sprintf_s(b, "%ls+0x%llx ", w.c_str(), (unsigned long long)(q - (uintptr_t)hm));
        frames += b;
        shown++;
    }
    return frames;
}

LONG CALLBACK DiagVeh(PEXCEPTION_POINTERS ep) {
    static std::atomic<int> count{0};
    auto er = ep->ExceptionRecord;
    DWORD c = er->ExceptionCode;
    bool cpp = c == 0xE06D7363;
    bool fatal = c == 0xC0000005 || c == 0xC0000409 || c == 0xC000001D || c == 0xC0000094 || c == 0xC00000FD;
    if ((cpp && !g_trace) || (!cpp && !fatal)) return EXCEPTION_CONTINUE_SEARCH;
    if (count.fetch_add(1) >= 60) return EXCEPTION_CONTINUE_SEARCH;
    char type[80] = "";
    if (cpp) CppTypeName(er, type, sizeof type);
    auto nt = (IMAGE_NT_HEADERS*)(g_base + ((IMAGE_DOS_HEADER*)g_base)->e_lfanew);
    uintptr_t size = nt->OptionalHeader.SizeOfImage, ip = (uintptr_t)er->ExceptionAddress;
    bool inExe = ip >= g_base && ip < g_base + size;
    Log("EXCEPTION code=0x%08lx type=%s thread=%lu at=%s+0x%llx exe-frames: %s", c, type, GetCurrentThreadId(), inExe ? "NSUNS4" : "other",
        (unsigned long long)(inExe ? ip - g_base : ip), ModuleFrames(ep->ContextRecord->Rsp).c_str());
    return EXCEPTION_CONTINUE_SEARCH;
}

}

void InstallDiagnostics() {
    AddVectoredExceptionHandler(1, DiagVeh);
}

}
