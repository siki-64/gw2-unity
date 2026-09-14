using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal struct TraitDefinition
{
    [FieldOffset(0x000)] internal Unknown000Storage Unknown000;
    [StructLayout(LayoutKind.Sequential, Size = 0x14)] internal struct Unknown000Storage { }
    [FieldOffset(0x014)] internal TraitContentId Id;
    [FieldOffset(0x018)] internal Unknown018Storage Unknown018;
    [StructLayout(LayoutKind.Sequential, Size = 0x58)] internal struct Unknown018Storage { }
    [FieldOffset(0x070)] internal uint Tier;
}
