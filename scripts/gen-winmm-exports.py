import os
import sys

import pefile

SYSTEM_WINMM = os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "System32", "winmm.dll")
DEFAULT_OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src", "native", "winmm_exports.inc")


def export_line(symbol):
    target = r"C:\\Windows\\System32\\winmm"
    if symbol.name:
        name = symbol.name.decode()
        return f'#pragma comment(linker, "/export:{name}={target}.{name},@{symbol.ordinal}")'
    return f'#pragma comment(linker, "/export:ord{symbol.ordinal}={target}.#{symbol.ordinal},@{symbol.ordinal},NONAME")'


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT
    pe = pefile.PE(SYSTEM_WINMM)
    symbols = sorted((s for s in pe.DIRECTORY_ENTRY_EXPORT.symbols if not s.forwarder), key=lambda s: s.ordinal)
    with open(out, "w", newline="\n") as f:
        f.write("\n".join(export_line(s) for s in symbols) + "\n")
    print(len(symbols), "exports ->", out)


if __name__ == "__main__":
    main()
