using System.Runtime.InteropServices;

namespace Kawarimi.Host;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct NativeApi
{
    public int Version;
    public delegate* unmanaged[Cdecl]<byte*, void> Log;
    public delegate* unmanaged[Cdecl]<void> Clear;
    public delegate* unmanaged[Cdecl]<byte*, int, byte*, byte*, void> AddSkin;
    public delegate* unmanaged[Cdecl]<void> Commit;
    public delegate* unmanaged[Cdecl]<int> IsActive;
    public char* GameDir;
    public delegate* unmanaged[Cdecl]<byte*, int, void> SetOption;
    public delegate* unmanaged[Cdecl]<byte*, int, byte*, byte*, byte*, void> AddSkin2;
    public delegate* unmanaged[Cdecl]<byte*, byte*, void> SetName;
}
