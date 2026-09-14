using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0xD8)]
internal unsafe struct PvpGearProvider
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal uint Unknown008;
    [FieldOffset(0x030)] internal uint Unknown030;
    [FieldOffset(0x038)] internal ChCliPlayer* OwnerPlayer;
    [FieldOffset(0x040)] internal nint RuneDefinition;
    [FieldOffset(0x048)] internal nint RelicDefinition;
    [FieldOffset(0x050)] internal nint AmuletDefinition;
    [FieldOffset(0x058)] internal nint Unknown058;
    [FieldOffset(0x060)] internal PvpHeroDefinition* PvpHeroDefinition;
    [FieldOffset(0x068)] internal nint CombinedRank06Definition;
    [FieldOffset(0x070)] internal byte Flags;
    [FieldOffset(0x078)] internal nint IncrementalRankDefinition;
    [FieldOffset(0x080)] internal PvpRatingValueArray RatingValues;
    [FieldOffset(0x0B0)] internal nint CombinedRank0ADefinition;
    [FieldOffset(0x0B8)] internal PvpSigilDefinitionArray SigilDefinitions;
    [InlineArray(4)]
    internal struct PvpSigilDefinitionArray
    {
        internal nint Element0;
    }
    [InlineArray(12)]
    internal struct PvpRatingValueArray
    {
        internal uint Element0;
    }
}
[StructLayout(LayoutKind.Explicit)]
internal struct PvpRankDefinition
{
    [FieldOffset(0x28)] internal uint Id;
}
[StructLayout(LayoutKind.Explicit, Size = 0x18)]
internal struct PvpHeroDefinition
{
    [FieldOffset(0x00)] internal Unknown000Storage Unknown000;
    [FieldOffset(0x10)] internal uint ContentRequestType;
    [FieldOffset(0x14)] internal uint Id;
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    internal struct Unknown000Storage { }
}
