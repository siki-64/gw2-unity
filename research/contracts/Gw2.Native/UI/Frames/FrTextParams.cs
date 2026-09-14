using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0x40)]
internal struct FrTextParams
{
    [FieldOffset(0x00)]
    internal uint Flags;
    [FieldOffset(0x04)]
    internal uint Reserved04;
    [FieldOffset(0x08)]
    internal nint Bounds;
    [FieldOffset(0x10)]
    internal nint Chars;
    [FieldOffset(0x18)]
    internal nint LineData;
    [FieldOffset(0x20)]
    internal nint MaterialData;
    [FieldOffset(0x28)]
    internal uint MaterialByteCount;
    [FieldOffset(0x2C)]
    internal uint Reserved2C;
    [FieldOffset(0x30)]
    internal nint Offset;
    [FieldOffset(0x38)]
    internal float FontHeightScaleAddend;
    [FieldOffset(0x3C)]
    internal uint Reserved3C;
}
[StructLayout(LayoutKind.Explicit, Size = 0x20)]
internal struct FrTextLineData
{
    [FieldOffset(0x08)]
    internal nint Records;
    [FieldOffset(0x10)]
    internal uint Capacity;
    [FieldOffset(0x14)]
    internal uint Count;
    [FieldOffset(0x18)]
    internal uint Reserved18;
}
