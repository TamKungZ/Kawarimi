#include "kawarimi.h"
#include "hook.h"

namespace kw {

namespace {

decltype(&connect) g_origConnect = nullptr;

bool IsLocal(const sockaddr* sa) {
    if (sa->sa_family != AF_INET) return sa->sa_family != AF_INET6;
    auto b = (const uint8_t*)&((const sockaddr_in*)sa)->sin_addr;
    return b[0] == 127 || b[0] == 10 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] < 32);
}

}

int WSAAPI Hook_Connect(SOCKET s, const sockaddr* name, int len) {
    if (name && !IsLocal(name) && !g_online.exchange(true)) {
        int policy = g_onlinePolicy;
        if (policy == 0) g_active = false;
        Log(policy == 0   ? "ONLINE: remote connect seen, skins disabled for this session"
            : policy == 1 ? "ONLINE: remote connect seen, only model and texture files of replacing skins are used for this session"
                          : "ONLINE: remote connect seen, skins unchanged (online = all)");
    }
    return g_origConnect ? g_origConnect(s, name, len) : SOCKET_ERROR;
}

void InstallOnlineWatch(HMODULE exe) {
    HMODULE ws = LoadLibraryW(L"ws2_32.dll");
    g_origConnect = (decltype(&connect))GetProcAddress(ws, "connect");
    int c = Skip("iat") ? 0 : PatchIat(exe, (void*)g_origConnect, (void*)Hook_Connect);
    Log("online watch IAT slots: connect=%d", c);
}

}
