using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x68)]
internal struct ChCliEndurance
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal nint Unknown008Vtable;
    [FieldOffset(0x010)] internal uint Unknown010;
    [FieldOffset(0x014)] internal uint Unknown014;
    [FieldOffset(0x018)] internal uint Unknown018;
    [FieldOffset(0x01C)] internal uint Unknown01C;
    [FieldOffset(0x020)] internal uint Unknown020;
    [FieldOffset(0x024)] internal uint Unknown024;
    [FieldOffset(0x028)] internal nint Unknown028;
    [FieldOffset(0x030)] internal Unknown030Storage Unknown030;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Unknown030Storage { }
    [FieldOffset(0x038)] internal Unknown038Storage Unknown038;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Unknown038Storage { }
    [FieldOffset(0x040)] internal nint Unknown040;
    [FieldOffset(0x048)] internal nint Unknown048;
    [FieldOffset(0x050)] internal nint Unknown050;
    [FieldOffset(0x058)] internal nint Unknown058;
    [FieldOffset(0x060)] internal Unknown060Storage Unknown060;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Unknown060Storage { }
}
