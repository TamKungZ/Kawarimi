#include "kawarimi.h"
#include <cstdarg>
#include <cstdio>
#include <share.h>

namespace kw {

std::atomic<const Snapshot*> g_snap{nullptr};
std::atomic<bool> g_active{true};
std::atomic<int> g_onlinePolicy{1};
std::atomic<bool> g_online{false};
int g_strategy = 0;
bool g_trace = false;
uintptr_t g_base = 0;
std::wstring g_gameDir;

namespace {
FILE* g_log = nullptr;
std::string g_skip;
}

void OpenLog(const std::wstring& path) {
    g_log = _wfsopen(path.c_str(), L"w", _SH_DENYNO);
}

void Log(const char* fmt, ...) {
    char line[2048];
    va_list a;
    va_start(a, fmt);
    vsnprintf(line, sizeof line, fmt, a);
    va_end(a);
    if (g_log) {
        fputs(line, g_log);
        fputc('\n', g_log);
        fflush(g_log);
    }
    ConsoleLine(line);
}

std::string ReadSmallFile(const std::wstring& path) {
    FILE* f = _wfsopen(path.c_str(), L"rb", _SH_DENYNO);
    if (!f) return "";
    char b[512] = {0};
    fread(b, 1, sizeof b - 1, f);
    fclose(f);
    return b;
}

void LoadSkipFlags(const std::wstring& path) {
    g_skip = ReadSmallFile(path);
}

bool Skip(const char* word) {
    return g_skip.find(word) != std::string::npos;
}

bool NewCodesAllowed() {
    return g_active && !(g_online && g_onlinePolicy == 1);
}

bool VisualOnly() {
    return g_online && g_onlinePolicy == 1;
}

bool Rd(const void* p, void* out, size_t n) {
    SIZE_T got = 0;
    return ReadProcessMemory(GetCurrentProcess(), p, out, n, &got) && got == n;
}

}
