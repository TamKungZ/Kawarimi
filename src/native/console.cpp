#include "kawarimi.h"
#include <cstdio>

namespace kw {

namespace {

HANDLE g_console = nullptr;

BOOL WINAPI IgnoreCtrl(DWORD) {
    return TRUE;
}

WORD ColorFor(const char* line) {
    if (strncmp(line, "EXCEPTION", 9) == 0 || strstr(line, "FAILED") || strstr(line, "NOT installed")) return FOREGROUND_RED | FOREGROUND_INTENSITY;
    if (strncmp(line, "COSTUME", 7) == 0) return FOREGROUND_GREEN | FOREGROUND_BLUE | FOREGROUND_INTENSITY;
    if (strncmp(line, "HOTKEY", 6) == 0 || strstr(line, "VFS load")) return FOREGROUND_GREEN | FOREGROUND_INTENSITY;
    if (strncmp(line, "[managed]", 9) == 0) return FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_INTENSITY;
    return FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE;
}

bool Verbose(const char* line) {
    static const char* prefixes[] = {"VFS find", "[t=", "RECORD", "TABLE", "EntryLookup", "WATCH", "capture:"};
    for (const char* p : prefixes)
        if (strncmp(line, p, strlen(p)) == 0) return true;
    return false;
}

}

void OpenConsole() {
    if (!AllocConsole()) return;
    SetConsoleTitleW(L"Kawarimi - log (F7 next skin, F8 off)");
    SetConsoleCtrlHandler(IgnoreCtrl, TRUE);
    if (HWND w = GetConsoleWindow()) {
        if (HMENU m = GetSystemMenu(w, FALSE)) DeleteMenu(m, SC_CLOSE, MF_BYCOMMAND);
    }
    g_console = GetStdHandle(STD_OUTPUT_HANDLE);
}

void ConsoleLine(const char* line) {
    if (!g_console || Verbose(line)) return;
    SetConsoleTextAttribute(g_console, ColorFor(line));
    DWORD n;
    WriteConsoleA(g_console, line, (DWORD)strlen(line), &n, nullptr);
    WriteConsoleA(g_console, "\r\n", 2, &n, nullptr);
}

}
