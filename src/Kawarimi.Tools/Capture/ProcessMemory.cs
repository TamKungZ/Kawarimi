using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Kawarimi.Tools.Capture;

internal sealed class ProcessMemory : IDisposable
{
    const uint VmRead = 0x0010, QueryInformation = 0x0400;
    const uint MemCommit = 0x1000, PageNoAccess = 0x01, PageGuard = 0x100;
    public const uint MemPrivate = 0x20000;

    [StructLayout(LayoutKind.Sequential)]
    struct MemoryBasicInformation
    {
        public nint BaseAddress, AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nuint RegionSize;
        public uint State, Protect, Type;
    }

    [DllImport("kernel32", SetLastError = true)]
    static extern nint OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32", SetLastError = true)]
    static extern bool CloseHandle(nint h);

    [DllImport("kernel32", SetLastError = true)]
    static extern bool ReadProcessMemory(nint h, nint addr, byte[] buf, nuint size, out nuint read);

    [DllImport("kernel32", SetLastError = true)]
    static extern nuint VirtualQueryEx(nint h, nint addr, out MemoryBasicInformation mbi, nuint len);

    readonly nint _handle;

    public ProcessMemory(Process p)
    {
        Base = p.MainModule!.BaseAddress;
        _handle = OpenProcess(VmRead | QueryInformation, false, p.Id);
        if (_handle == 0) throw new InvalidOperationException("OpenProcess failed (" + Marshal.GetLastWin32Error() + ")");
    }

    public nint Base { get; }

    public byte[]? Read(nint addr, int n)
    {
        var b = new byte[n];
        return ReadProcessMemory(_handle, addr, b, (nuint)n, out var got) && (int)got == n ? b : null;
    }

    public long ReadI64(nint a) => Read(a, 8) is { } b ? BitConverter.ToInt64(b) : 0;

    public int ReadI32(nint a) => Read(a, 4) is { } b ? BitConverter.ToInt32(b) : 0;

    public IEnumerable<(nint addr, long size, uint type)> Regions()
    {
        nint a = 0;
        while (VirtualQueryEx(_handle, a, out var m, (nuint)Marshal.SizeOf<MemoryBasicInformation>()) != 0)
        {
            if (m.State == MemCommit && (m.Protect & (PageNoAccess | PageGuard)) == 0) yield return (m.BaseAddress, (long)m.RegionSize, m.Type);
            long next = (long)m.BaseAddress + (long)m.RegionSize;
            if (next <= (long)a) yield break;
            a = (nint)next;
        }
    }

    public void Dispose() => CloseHandle(_handle);
}
