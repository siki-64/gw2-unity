using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x0A)]
internal struct PvpIncrementalGearUpdate
{
    internal const int PlayerListIndexOffset = 0x02;
    internal const int ContentIdOffset = 0x06;

    [FieldOffset(0x00)] internal ushort Unknown00;
    [FieldOffset(0x02)] internal uint PlayerListIndex;
    [FieldOffset(0x06)] internal uint ContentId;
}

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x0B)]
internal struct PvpHeroUpdate
{
    [FieldOffset(0x00)] internal ushort Unknown00;
    [FieldOffset(0x02)] internal uint PlayerListIndex;
    [FieldOffset(0x06)] internal uint PvpHeroContentId;
    // PvpGearProvider::SetPvpHero maps this byte to provider Flags bit 1. The
    // boolean's higher-level meaning is not yet established.
    [FieldOffset(0x0A)] internal byte Unknown0A;
}

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x27)]
internal unsafe struct PvpCombinedGearUpdate
{
    [FieldOffset(0x00)] internal ushort Unknown00;
    [FieldOffset(0x02)] internal uint PlayerListIndex;
    [FieldOffset(0x06)] internal uint RankDefinition06Id;
    [FieldOffset(0x0A)] internal uint RankDefinition0AId;
    [FieldOffset(0x0E)] internal byte RankFlags;
    [FieldOffset(0x0F)] internal uint RuneContentId;
    [FieldOffset(0x13)] internal uint RelicContentId;
    [FieldOffset(0x17)] internal uint AmuletContentId;
    [FieldOffset(0x1B)] internal uint* SigilContentIds;
    [FieldOffset(0x23)] internal Unknown023Storage Unknown023;
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x04)]
    internal struct Unknown023Storage { }
}

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x06)]
internal struct PvpProviderRemovalUpdate
{
    [FieldOffset(0x00)] internal ushort Unknown00;
    [FieldOffset(0x02)] internal uint PlayerListIndex;
}

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x0A)]
internal struct PvpIncrementalRankUpdate
{
    [FieldOffset(0x00)] internal ushort Unknown00;
    [FieldOffset(0x02)] internal uint PlayerListIndex;
    [FieldOffset(0x06)] internal uint RankDefinitionId;
}

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x0F)]
internal struct PvpCombinedRankUpdate
{
    [FieldOffset(0x00)] internal ushort Unknown00;
    [FieldOffset(0x02)] internal uint PlayerListIndex;
    [FieldOffset(0x06)] internal uint RankDefinition06Id;
    [FieldOffset(0x0A)] internal uint RankDefinition0AId;
    [FieldOffset(0x0E)] internal byte RankFlags;
}
