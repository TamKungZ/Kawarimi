#include "kawarimi.h"

namespace {

char g_fakeSlots[8][8] = {{0}, {'1', 't', 's', 't'}, {'2', 't', 's', 't'}, {0}, {'4', 't', 's', 't'}};

const char* FakeGetSlot(int chr, int idx) {
    if (chr != 5) return nullptr;
    if (idx < 0 || idx > 3) return g_fakeSlots[1];
    return g_fakeSlots[idx + 1][0] ? g_fakeSlots[idx + 1] : g_fakeSlots[1];
}

const char* FakeCode(int chr, char out[9]) {
    if (chr != 5) return nullptr;
    memcpy(out, "1tst\0\0\0\0", 9);
    return out;
}

}

extern "C" {

__declspec(dllexport) void kw_test_use_fake_game() {
    kw::g_origGetSlot = FakeGetSlot;
    kw::CodeOfChar = FakeCode;
}

__declspec(dllexport) const char* kw_test_get_slot(int chr, int idx) {
    return kw::Hook_GetSlot(chr, idx);
}

__declspec(dllexport) int kw_test_redirect(const wchar_t* in, wchar_t* out, int cap) {
    std::wstring r;
    if (!kw::Redirect(in, r)) return 0;
    wcsncpy_s(out, cap, r.c_str(), _TRUNCATE);
    return 1;
}

__declspec(dllexport) void kw_test_remote_connect() {
    sockaddr_in a{};
    a.sin_family = AF_INET;
    a.sin_addr.S_un.S_addr = htonl(0x08080808);
    kw::Hook_Connect(INVALID_SOCKET, (sockaddr*)&a, sizeof a);
}
}
