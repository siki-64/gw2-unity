using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x60)]
internal struct ChCliOrder
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal nint InterfaceVtable;
    [FieldOffset(0x010)] internal nint Unknown010;
    [FieldOffset(0x018)] internal uint Unknown018;
    [FieldOffset(0x01C)] internal Unknown01CStorage Unknown01C;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)]
    internal struct Unknown01CStorage { }
    [FieldOffset(0x020)] internal uint Unknown020;
    [FieldOffset(0x024)] internal int Unknown024;
    [FieldOffset(0x028)] internal nint Unknown028;
    [FieldOffset(0x030)] internal nint Unknown030;
    [FieldOffset(0x038)] internal nint Unknown038;
    [FieldOffset(0x040)] internal uint Unknown040;
    [FieldOffset(0x044)] internal Unknown044Storage Unknown044;
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    internal struct Unknown044Storage { }
    [FieldOffset(0x054)] internal ulong Unknown054;
    [FieldOffset(0x05C)] internal Unknown05CStorage Unknown05C;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)]
    internal struct Unknown05CStorage { }
}
