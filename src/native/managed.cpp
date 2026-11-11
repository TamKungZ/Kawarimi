#include "kawarimi.h"

namespace kw {

namespace {

struct NativeApi {
    int version;
    void (*log)(const char*);
    void (*clear)();
    void (*add_skin)(const char* charCode, int charId, const char* skinCode, const char* dir);
    void (*commit)();
    int (*is_active)();
    const wchar_t* gameDir;
    void (*set_option)(const char* key, int value);
    void (*add_skin2)(const char* charCode, int charId, const char* skinCode, const char* dir, const char* replaces);
    void (*set_name)(const char* family, const char* name);
};

Snapshot* g_pending = nullptr;

void Api_Log(const char* m) {
    Log("[managed] %s", m);
}

void Api_Clear() {
    delete g_pending;
    g_pending = new Snapshot();
}

void Api_Add2(const char* charCode, int charId, const char* skinCode, const char* dir, const char* replaces) {
    if (!g_pending) g_pending = new Snapshot();
    Skin k{};
    strncpy_s(k.code, 8, skinCode, _TRUNCATE);
    k.dir = dir ? dir : "";
    k.replaces = replaces ? replaces : "";
    if (charCode && *charCode) g_pending->byCode[charCode].push_back(k);
    if (charId >= 0) g_pending->byId[charId].push_back(k);
}

void Api_Add(const char* charCode, int charId, const char* skinCode, const char* dir) {
    Api_Add2(charCode, charId, skinCode, dir, nullptr);
}

void Api_Commit() {
    g_snap.store(g_pending);
    g_pending = nullptr;
}

int Api_IsActive() {
    return g_active ? 1 : 0;
}

void Api_SetOption(const char* key, int value) {
    ApplyOption(key ? key : "", value);
}

void Api_SetName(const char* family, const char* name) {
    if (family && name) SetFamilyName(family, name);
}

NativeApi g_api{4, Api_Log, Api_Clear, Api_Add, Api_Commit, Api_IsActive, nullptr, Api_SetOption, Api_Add2, Api_SetName};

using hostfxr_init_fn = int (*)(const wchar_t*, const void*, void**);
using hostfxr_get_delegate_fn = int (*)(void*, int, void**);
using load_assembly_fn = int (*)(const wchar_t*, const wchar_t*, const wchar_t*, const wchar_t*, void*, void**);
using init_fn = int (*)(NativeApi*);
constexpr int kLoadAssemblyAndGetFunctionPointer = 5;

bool NewerVersion(const std::wstring& a, const std::wstring& b) {
    int ma = std::stoi(a), mb = std::stoi(b);
    return ma != mb ? ma > mb : a > b;
}

std::wstring FindHostFxr() {
    wchar_t pf[MAX_PATH];
    DWORD n = GetEnvironmentVariableW(L"DOTNET_ROOT", pf, MAX_PATH);
    std::wstring root = (n > 0 && n < MAX_PATH) ? std::wstring(pf) : std::wstring(L"C:\\Program Files\\dotnet");
    WIN32_FIND_DATAW fd;
    std::wstring best;
    HANDLE h = FindFirstFileW((root + L"\\host\\fxr\\*").c_str(), &fd);
    if (h == INVALID_HANDLE_VALUE) return L"";
    do {
        if (fd.cFileName[0] == L'.' || !(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)) continue;
        std::wstring v = fd.cFileName;
        if (best.empty() || NewerVersion(v, best)) best = v;
    } while (FindNextFileW(h, &fd));
    FindClose(h);
    return best.empty() ? L"" : root + L"\\host\\fxr\\" + best + L"\\hostfxr.dll";
}

}

void ApplyOption(const std::string& k, int value) {
    if (k == "strategy")
        g_strategy = value;
    else if (k == "trace")
        g_trace = value != 0;
    else if (k == "watchChar")
        g_watchChr = value;
    else if (k == "online")
        g_onlinePolicy = value;
    else
        Log("unknown option %s", k.c_str());
}

bool BootManaged(const std::wstring& dir) {
    auto fxrPath = FindHostFxr();
    HMODULE fxr = fxrPath.empty() ? nullptr : LoadLibraryW(fxrPath.c_str());
    if (!fxr) {
        Log("hostfxr not found");
        return false;
    }
    auto init = (hostfxr_init_fn)GetProcAddress(fxr, "hostfxr_initialize_for_runtime_config");
    auto getDel = (hostfxr_get_delegate_fn)GetProcAddress(fxr, "hostfxr_get_runtime_delegate");
    if (!init || !getDel) {
        Log("hostfxr exports missing");
        return false;
    }
    void* ctx = nullptr;
    int rc = init((dir + L"\\Kawarimi.Host.runtimeconfig.json").c_str(), nullptr, &ctx);
    if (rc < 0 || !ctx) {
        Log("hostfxr init failed 0x%x", rc);
        return false;
    }
    void* fn = nullptr;
    rc = getDel(ctx, kLoadAssemblyAndGetFunctionPointer, &fn);
    if (rc != 0 || !fn) {
        Log("get delegate failed 0x%x", rc);
        return false;
    }
    void* entry = nullptr;
    rc = ((load_assembly_fn)fn)((dir + L"\\Kawarimi.Host.dll").c_str(), L"Kawarimi.Host.Entry, Kawarimi.Host", L"Init", (const wchar_t*)(intptr_t)-1, nullptr,
                                &entry);
    if (rc != 0 || !entry) {
        Log("load Kawarimi.Host failed 0x%x", rc);
        return false;
    }
    g_api.gameDir = g_gameDir.c_str();
    int r = ((init_fn)entry)(&g_api);
    Log("managed Init returned %d", r);
    return r == 0;
}

}
