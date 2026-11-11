namespace Kawarimi.Formats;

public sealed class XfbinChunk
{
    public uint MapIndex { get; set; }
    public ushort Version { get; set; }
    public ushort Flags { get; set; }
    public byte[] Data { get; set; } = [];
}
