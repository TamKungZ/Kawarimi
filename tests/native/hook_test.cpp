#include <windows.h>
#include <cstdio>
#include <cstring>
#include "../../src/native/hook.h"

static int (*g_orig)(int, int) = nullptr;
static int Detour(int a, int b) {
    return a == 1 ? 1000 + b : g_orig(a, b);
}

int main() {
    const uint8_t code[] = {0x48, 0x89, 0x5c, 0x24, 0x08, 0x57, 0x48, 0x83, 0xec, 0x20, 0x89, 0xd0,
                            0x48, 0x83, 0xc4, 0x20, 0x5f, 0x48, 0x8b, 0x5c, 0x24, 0x08, 0xc3};
    auto mem = (uint8_t*)VirtualAlloc(nullptr, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    memcpy(mem, code, sizeof code);
    auto fn = (int (*)(int, int))mem;
    int fails = 0;
    auto check = [&](const char* what, bool ok) {
        printf("%s %s\n", ok ? "PASS" : "FAIL", what);
        fails += !ok;
    };

    check("unhooked fn returns arg2", fn(1, 7) == 7);
    uint8_t wrong[10] = {0};
    void* o;
    check("wrong prologue is rejected", !kw::InstallHook(mem, wrong, sizeof wrong, (void*)Detour, &o));
    check("hook installs", kw::InstallHook(mem, code, 10, (void*)Detour, (void**)&g_orig));
    check("detour intercepts (a==1)", fn(1, 7) == 1007);
    check("trampoline runs original (a!=1)", fn(2, 7) == 7);

    int n = kw::PatchIat(GetModuleHandleW(nullptr), (void*)GetTickCount, (void*)GetTickCount);
    printf("IAT walk ok (slots=%d)\n", n);
    printf(fails ? "FAILED\n" : "ALL PASSED\n");
    return fails;
}
