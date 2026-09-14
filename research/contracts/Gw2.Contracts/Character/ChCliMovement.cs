using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0xB0)]
internal unsafe struct ChCliMovement
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal ChCliCharacter* OwnerCharacter;
    [FieldOffset(0x010)] internal nint Unknown010;
    [FieldOffset(0x018)] internal nint Unknown018;
    [FieldOffset(0x020)] internal nint Unknown020;
    [FieldOffset(0x028)] internal nint Unknown028;
    [FieldOffset(0x030)] internal nint Unknown030;
    [FieldOffset(0x038)] internal nint Unknown038;
    [FieldOffset(0x040)] internal nint Unknown040;
    [FieldOffset(0x048)] internal nint Unknown048;
    [FieldOffset(0x050)] internal nint Unknown050;
    [FieldOffset(0x058)] internal nint Unknown058;
    [FieldOffset(0x060)] internal nint Unknown060;
    [FieldOffset(0x068)] internal nint Unknown068;
    [FieldOffset(0x070)] internal nint Unknown070;
    [FieldOffset(0x078)] internal uint Unknown078;
    [FieldOffset(0x07C)] internal uint Unknown07C;
    [FieldOffset(0x080)] internal Unknown080Storage Unknown080;
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    internal struct Unknown080Storage { }
    [FieldOffset(0x090)] internal Unknown090Storage Unknown090;
    [StructLayout(LayoutKind.Sequential, Size = 0x20)]
    internal struct Unknown090Storage { }
}
