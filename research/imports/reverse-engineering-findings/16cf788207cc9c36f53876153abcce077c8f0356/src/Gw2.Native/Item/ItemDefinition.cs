using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit)]
internal struct ItemDefinition
{
    [FieldOffset(0x00)] internal Unknown000Storage Unknown000;
    [StructLayout(LayoutKind.Sequential, Size = 0x28)] internal struct Unknown000Storage { }
    [FieldOffset(0x28)] internal uint Id;
    [FieldOffset(0x2C)] internal Unknown02CStorage Unknown02C;
    [StructLayout(LayoutKind.Sequential, Size = 0x34)] internal struct Unknown02CStorage { }
    [FieldOffset(0x60)] internal ItemRarity Rarity;
}
