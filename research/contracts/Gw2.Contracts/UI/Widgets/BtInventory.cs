using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x60)]
internal struct BtInventory
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal Interface008Storage Interface008;
    [StructLayout(LayoutKind.Sequential, Size = 0x50)] internal struct Interface008Storage { }
    [FieldOffset(0x58)] internal int Unknown58;
    [FieldOffset(0x5C)] internal uint Unknown5C;
}
