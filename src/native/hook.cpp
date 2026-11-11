#include "hook.h"
#include <windows.h>
#include <cstring>

namespace kw {

namespace {

uint8_t* AllocNear(void* nearAddr, size_t size) {
    SYSTEM_INFO si;
    GetSystemInfo(&si);
    uintptr_t gran = si.dwAllocationGranularity, base = (uintptr_t)nearAddr & ~(gran - 1);
    for (uintptr_t d = gran; d < 0x70000000; d += gran) {
        for (int dir = 0; dir < 2; dir++) {
            uintptr_t a = dir ? base + d : base - d;
            void* p = VirtualAlloc((void*)a, size, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
            if (p) return (uint8_t*)p;
        }
    }
    return nullptr;
}

void EmitAbsJmp(uint8_t* at, void* dest) {
    at[0] = 0xFF;
    at[1] = 0x25;
    *(uint32_t*)(at + 2) = 0;
    *(void**)(at + 6) = dest;
}

}

bool InstallHook(void* target, const uint8_t* expect, size_t stolen, void* detour, void** orig) {
    uint8_t* t = (uint8_t*)target;
    if (stolen < 5 || stolen > 32 || memcmp(t, expect, stolen) != 0) return false;
    uint8_t* block = AllocNear(target, 128);
    if (!block) return false;
    uint8_t* tramp = block;
    uint8_t* stub = block + 64;
    memcpy(tramp, t, stolen);
    EmitAbsJmp(tramp + stolen, t + stolen);
    EmitAbsJmp(stub, detour);
    FlushInstructionCache(GetCurrentProcess(), block, 128);
    *orig = tramp;

    DWORD old;
    if (!VirtualProtect(t, 8, PAGE_EXECUTE_READWRITE, &old)) return false;
    uint8_t patch[8];
    memcpy(patch, t, 8);
    patch[0] = 0xE9;
    *(int32_t*)(patch + 1) = (int32_t)(stub - (t + 5));
    if (((uintptr_t)t & 7) == 0)
        InterlockedExchange64((volatile LONG64*)t, *(LONG64*)patch);
    else
        memcpy(t, patch, 5);
    VirtualProtect(t, 8, old, &old);
    FlushInstructionCache(GetCurrentProcess(), t, 8);
    return true;
}

int PatchIat(void* module, void* original, void* replacement) {
    auto base = (uint8_t*)module;
    auto nt = (IMAGE_NT_HEADERS*)(base + ((IMAGE_DOS_HEADER*)base)->e_lfanew);
    auto& dir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (!dir.VirtualAddress) return 0;
    int n = 0;
    for (auto d = (IMAGE_IMPORT_DESCRIPTOR*)(base + dir.VirtualAddress); d->Name; d++) {
        for (auto thunk = (void**)(base + d->FirstThunk); *thunk; thunk++) {
            if (*thunk != original) continue;
            DWORD old;
            if (VirtualProtect(thunk, sizeof(void*), PAGE_READWRITE, &old)) {
                *thunk = replacement;
                VirtualProtect(thunk, sizeof(void*), old, &old);
                n++;
            }
        }
    }
    return n;
}

}
