import os
import struct
import sys

import numpy as np
from capstone import CS_ARCH_X86, CS_MODE_64, Cs

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BASE = 0x140000000
USAGE = "usage: exe_tools.py f <va> [n] | callers <va> | str <text> | dis <va> [n]"


def read_env(key):
    if os.environ.get(key):
        return os.environ[key]
    path = os.path.join(ROOT, ".env")
    if os.path.exists(path):
        for line in open(path, encoding="utf-8"):
            name, _, value = line.partition("=")
            if name.strip() == key:
                return value.strip().strip('"')
    sys.exit(f"{key} is not set; add it to {path}")


EXE = os.path.join(read_env("KAWARIMI_GAME_DIR"), "NSUNS4.exe")
DATA = open(EXE, "rb").read()


def load_sections():
    pe = struct.unpack_from("<I", DATA, 0x3C)[0]
    count = struct.unpack_from("<H", DATA, pe + 6)[0]
    opt = struct.unpack_from("<H", DATA, pe + 20)[0]
    sections = []
    for i in range(count):
        s = DATA[pe + 24 + opt + i * 40:][:40]
        vsize, va, rsize, raw = struct.unpack_from("<IIII", s, 8)
        sections.append((s[:8].rstrip(b"\0").decode(), va, vsize, raw, rsize))
    return sections


SECTIONS = load_sections()
TEXT_START = SECTIONS[0][3]
TEXT_END = TEXT_START + SECTIONS[0][4]


def file_offset(va):
    r = va - BASE
    for _, a, vsize, raw, rsize in SECTIONS:
        if a <= r < a + max(vsize, rsize):
            return raw + r - a
    return None


def va_of(offset):
    for _, a, _, raw, rsize in SECTIONS:
        if raw <= offset < raw + rsize:
            return BASE + a + offset - raw
    return None


def build_index():
    t = np.frombuffer(DATA[TEXT_START:TEXT_END], dtype=np.uint8)
    idx = np.arange(len(t) - 8)
    tva = va_of(TEXT_START)

    def disp32(k):
        b = [t[idx + k + j].astype(np.int64) << (8 * j) for j in range(4)]
        return (b[0] | b[1] | b[2] | b[3]).astype(np.int32).astype(np.int64)

    rex = np.isin(t[idx], [0x40, 0x41, 0x44, 0x45, 0x48, 0x49, 0x4C, 0x4D])
    rex &= np.isin(t[idx + 1], [0x03, 0x2B, 0x39, 0x3B, 0x63, 0x81, 0x83, 0x85, 0x89, 0x8B, 0x8D, 0xC7, 0xFF])
    rex &= (t[idx + 2] & 0xC7) == 5
    plain = np.isin(t[idx], [0x80, 0x88, 0x89, 0x8A, 0x8B, 0x8D, 0x39, 0x3B, 0xC6, 0xFF]) & ((t[idx + 1] & 0xC7) == 5)
    rip_src = np.concatenate([idx[rex] + tva, idx[plain] + tva])
    rip_dst = np.concatenate([idx[rex] + tva + 7 + disp32(3)[rex], idx[plain] + tva + 6 + disp32(2)[plain]])
    call = np.isin(t[idx], [0xE8, 0xE9])
    call_src = idx[call] + tva
    call_dst = call_src + 5 + disp32(1)[call]
    return rip_src, rip_dst, call_src, call_dst


def load_index():
    cache = os.path.join(ROOT, "out", "re-index.npz")
    if os.path.exists(cache) and os.path.getmtime(cache) > os.path.getmtime(EXE):
        z = np.load(cache)
        return z["a"], z["b"], z["c"], z["d"]
    os.makedirs(os.path.dirname(cache), exist_ok=True)
    a, b, c, d = build_index()
    np.savez(cache, a=a, b=b, c=c, d=d)
    return a, b, c, d


RIP_SRC, RIP_DST, CALL_SRC, CALL_DST = load_index()


def xrefs(target):
    return [int(x) for x in RIP_SRC[RIP_DST == target]]


def calls_to(target):
    return [int(x) for x in CALL_SRC[CALL_DST == target]]


def func_range(va):
    for name, a, _, raw, rsize in SECTIONS:
        if name == ".pdata":
            for i in range(0, rsize, 12):
                begin, end, _ = struct.unpack_from("<III", DATA, raw + i)
                if begin <= va - BASE < end:
                    return BASE + begin, BASE + end
    return None


def disassemble(va, count=40, length=None):
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    o = file_offset(va)
    for k, ins in enumerate(md.disasm(DATA[o:o + (length or count * 8)], va)):
        if k >= count:
            break
        print(f"{ins.address:x}: {ins.mnemonic} {ins.op_str}")


def show_function(va, count):
    r = func_range(va)
    if not r:
        sys.exit("no .pdata entry for that address")
    start, end = r
    print(f"--- func {start:x}..{end:x}")
    disassemble(start, count, end - start)
    print("callers", [hex(x) for x in calls_to(start)], "lea", [hex(x) for x in xrefs(start)])


def find_strings(text):
    needle = text.encode()
    i = DATA.find(needle)
    while i != -1:
        va = va_of(i)
        value = DATA[i:i + 60].split(b"\0")[0]
        print(hex(va) if va else i, value, [hex(x) for x in xrefs(va)] if va else "")
        i = DATA.find(needle, i + 1)


def main(argv):
    if len(argv) < 3:
        sys.exit(USAGE)
    cmd, arg = argv[1], argv[2]
    n = int(argv[3]) if len(argv) > 3 else 200
    if cmd == "f":
        show_function(int(arg, 16), n)
    elif cmd == "callers":
        v = int(arg, 16)
        print("calls", [hex(x) for x in calls_to(v)], "lea", [hex(x) for x in xrefs(v)])
    elif cmd == "str":
        find_strings(arg)
    elif cmd == "dis":
        disassemble(int(arg, 16), n if len(argv) > 3 else 40)
    else:
        sys.exit(USAGE)


if __name__ == "__main__":
    main(sys.argv)
