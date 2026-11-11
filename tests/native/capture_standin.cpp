#include <windows.h>
#include <cstdio>
#include <cstdlib>
#include <cstring>

int main(int argc, char** argv) {
    for (int i = 1; i < argc; i++) {
        FILE* f = nullptr;
        if (fopen_s(&f, argv[i], "rb") || !f) {
            printf("cannot open %s\n", argv[i]);
            return 1;
        }
        fseek(f, 0, SEEK_END);
        long n = ftell(f);
        fseek(f, 0, SEEK_SET);
        size_t total = 0x20000 + n + 0x20000;
        unsigned char* buf = (unsigned char*)malloc(total);
        memset(buf, 0x5A, total);
        fread(buf + 0x20000, 1, n, f);
        fclose(f);
        memset(buf + 0x20000 + n, 0, 0x2000);
    }
    printf("ready\n");
    fflush(stdout);
    Sleep(120000);
    return 0;
}
