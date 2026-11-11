#include "kawarimi.h"
#include "hook.h"
#include "winmm_exports.inc"
#include <algorithm>

namespace kw {

namespace {

bool Exists(const std::wstring& path) {
    return GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES;
}

bool ConsoleWanted(const std::wstring& kdir) {
    std::string cfg = ReadSmallFile(kdir + L"\\config.json");
    cfg.erase(std::remove_if(cfg.begin(), cfg.end(), [](char c) { return c == ' ' || c == '\t' || c == '\r' || c == '\n'; }), cfg.end());
    return cfg.find("\"console\":false") == std::string::npos;
}

void InstallGameHooks(const std::wstring& kdir) {
    HMODULE exe = GetModuleHandleW(nullptr);
    wchar_t path[MAX_PATH];
    GetModuleFileNameW(exe, path, MAX_PATH);
    std::wstring name = path;
    name = name.substr(name.find_last_of(L'\\') + 1);
    if (_wcsicmp(name.c_str(), L"NSUNS4.exe") != 0) {
        Log("not NSUNS4.exe (%ls): game hooks skipped", name.c_str());
        return;
    }
    g_base = (uintptr_t)exe;
    if (!Skip("veh")) InstallDiagnostics();
    if (Skip("hooks")) {
        Log("skip: hooks");
    } else {
        InstallResearchHooks();
        InstallCostumeHooks();
    }
    InstallFileHooks(exe, Exists(kdir + L"\\vfslog.txt"));
    if (Exists(kdir + L"\\capture.txt")) InstallCapture(exe, kdir + L"\\capture");
    InstallOnlineWatch(exe);
    if (!Skip("maint")) StartMaintainer();
}

DWORD WINAPI Bootstrap(LPVOID self) {
    wchar_t p[MAX_PATH];
    GetModuleFileNameW((HMODULE)self, p, MAX_PATH);
    std::wstring dll = p;
    g_gameDir = dll.substr(0, dll.find_last_of(L'\\'));
    g_trace = GetEnvironmentVariableW(L"KAWARIMI_TRACE", nullptr, 0) > 0;
    std::wstring kdir = g_gameDir + L"\\kawarimi";
    CreateDirectoryW(kdir.c_str(), nullptr);
    LoadSkipFlags(kdir + L"\\skip.txt");
    std::string watch = ReadSmallFile(kdir + L"\\watch.txt");
    if (!watch.empty()) g_watchChr = atoi(watch.c_str());
    SetOverrideCallers(ReadSmallFile(kdir + L"\\ovmask.txt"));
    if (ConsoleWanted(kdir) && GetModuleHandleW(L"NSUNS4.exe")) OpenConsole();
    OpenLog(kdir + L"\\kawarimi.log");
    std::string skip = ReadSmallFile(kdir + L"\\skip.txt");
    if (!skip.empty()) Log("skip flags: %s", skip.c_str());
    Log("Kawarimi native starting (trace=%d)", (int)g_trace);
    InstallGameHooks(kdir);
    if (Skip("managed"))
        Log("skip: managed");
    else
        BootManaged(kdir);
    return 0;
}

}

}

BOOL WINAPI DllMain(HINSTANCE h, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(h);
        CreateThread(nullptr, 0, kw::Bootstrap, h, 0, nullptr);
    }
    return TRUE;
}
