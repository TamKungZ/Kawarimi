#include <windows.h>
#include <cstdio>
#include <string>

static int CountMissingExports(HMODULE proxy, int& total) {
    auto base = (uint8_t*)LoadLibraryW(L"C:\\Windows\\System32\\winmm.dll");
    auto nt = (IMAGE_NT_HEADERS*)(base + ((IMAGE_DOS_HEADER*)base)->e_lfanew);
    auto& dir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT];
    auto ex = (IMAGE_EXPORT_DIRECTORY*)(base + dir.VirtualAddress);
    auto names = (DWORD*)(base + ex->AddressOfNames);
    int missing = 0;
    total = 0;
    for (DWORD i = 0; i < ex->NumberOfNames; i++) {
        total++;
        if (!GetProcAddress(proxy, (const char*)(base + names[i]))) missing++;
    }
    return missing;
}

static std::string ReadText(const std::string& path) {
    FILE* f = nullptr;
    if (fopen_s(&f, path.c_str(), "rb") || !f) return "";
    char b[4096];
    size_t n = fread(b, 1, sizeof b - 1, f);
    b[n] = 0;
    fclose(f);
    return b;
}

int main(int argc, char** argv) {
    std::string dir = argc > 1 ? argv[1] : ".";
    auto h = LoadLibraryA((dir + "\\winmm.dll").c_str());
    if (!h) {
        printf("FAIL cannot load proxy (%lu)\n", GetLastError());
        return 1;
    }
    Sleep(4000);

    auto useFake = (void (*)())GetProcAddress(h, "kw_test_use_fake_game");
    auto getSlot = (const char* (*)(int, int))GetProcAddress(h, "kw_test_get_slot");
    auto redirect = (int (*)(const wchar_t*, wchar_t*, int))GetProcAddress(h, "kw_test_redirect");
    auto remote = (void (*)())GetProcAddress(h, "kw_test_remote_connect");
    int fails = 0;
    auto check = [&](const char* what, bool ok) {
        printf("%s %s\n", ok ? "PASS" : "FAIL", what);
        fails += !ok;
    };
    useFake();

    int total = 0, missing = CountMissingExports(h, total);
    printf("     (system winmm exports %d, missing from proxy %d)\n", total, missing);
    check("proxy forwards every winmm export", total > 100 && missing == 0);

    std::string report = ReadText(dir + "\\kawarimi\\mods-report.txt");
    check("host wrote mods-report.txt", !report.empty());
    check("report: json mod loaded", report.find("[Loaded] SampleSkin") != std::string::npos);
    check("report: code mod loaded", report.find("[Loaded] ExampleSkinMod") != std::string::npos);
    check("report: broken mod isolated", report.find("[Failed] Broken") != std::string::npos);

    check("built-in costume 0 untouched", std::string(getSlot(5, 0), 4) == "1tst");
    check("built-in costume 1 untouched", std::string(getSlot(5, 1), 4) == "2tst");
    check("built-in costume 3 untouched (skins never replace real slots)", std::string(getSlot(5, 3), 4) == "4tst");
    check("first skin takes the first free costume idx (2)", std::string(getSlot(5, 2), 4) == "9t01");
    check("second skin takes the next free idx (4)", std::string(getSlot(5, 4), 4) == "9t02");
    check("free idx after the last skin falls back to the game", std::string(getSlot(5, 5), 4) == "1tst");
    check("other characters are unaffected", getSlot(7, 3) == nullptr);

    wchar_t out[512];
    std::wstring want = std::wstring(dir.begin(), dir.end()) + L"\\mods\\SampleSkin\\files\\9t01bod1.xfbin";
    check("redirect skin file", redirect(L"data/spc/9t01bod1.xfbin", out, 512) && want == out);
    check("redirect is case-insensitive", redirect(L"G:\\Game\\DATA\\SPC\\9T01bod1.xfbin", out, 512));
    check("non-skin file not redirected", !redirect(L"data/spc/1tstbod1.xfbin", out, 512));
    check("skin file missing on disk not redirected", !redirect(L"data/spc/9t01nothere.xfbin", out, 512));

    remote();
    check("online: new-code skins are switched off", std::string(getSlot(5, 2), 4) == "1tst");
    check("online: new-code skin files are not served", !redirect(L"data/spc/9t01bod1.xfbin", out, 512));

    printf(fails ? "FAILED\n" : "ALL PASSED\n");
    return fails;
}
