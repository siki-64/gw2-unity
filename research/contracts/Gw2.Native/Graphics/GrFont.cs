using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0x60)]
internal struct GrFont
{
    internal const int SupportedBuild = (int)GameBuild.SupportedGameBuild;
    internal const int NativeSize = 0x60;
    internal const int RangeStride = 0x70;
    internal const uint SmallFontHeight = 15;
    internal const uint LargeFontHeight = 16;
    internal const int VtableOffset = 0x000;
    internal const int MetricCacheDirtyOffset = 0x010;
    internal const int ActiveRangeOffset = 0x018;
    internal const int SecondaryRangeOffset = 0x020;
    internal const int HeightOffset = 0x02C;
    internal const int Spacing38Offset = 0x038;
    internal const int DefaultAdvanceOffset = 0x03C;
    internal const int RangesOffset = 0x050;
    internal const int RangeCountOffset = 0x05C;
    [FieldOffset(VtableOffset)] internal nint Vtable;
    [FieldOffset(MetricCacheDirtyOffset)] internal uint MetricCacheDirty;
    [FieldOffset(ActiveRangeOffset)] internal nint ActiveRange;
    [FieldOffset(SecondaryRangeOffset)] internal nint SecondaryRange;
    [FieldOffset(HeightOffset)] internal uint Height;
    [FieldOffset(Spacing38Offset)] internal uint Spacing38;
    [FieldOffset(DefaultAdvanceOffset)] internal uint DefaultAdvance;
    [FieldOffset(RangesOffset)] internal nint Ranges;
    [FieldOffset(RangeCountOffset)] internal uint RangeCount;
}
internal enum GrFontRangeLoadState : uint
{
    Loaded = 0,
    Failed = 1,
    Unloaded = 2
}
[StructLayout(LayoutKind.Explicit, Size = 0x70)]
internal struct GrFontRange
{
    internal const int SupportedBuild = GrFont.SupportedBuild;
    internal const int NativeSize = 0x70;
    internal const int LoadStateOffset = 0x000;
    internal const int RangeAssetKeyOffset = 0x010;
    internal const int RangeAssetCapacityOffset = 0x018;
    internal const int RangeAssetCountOffset = 0x01C;
    internal const int StartCodeUnitOffset = 0x020;
    internal const int LastUse24Offset = 0x024;
    internal const int TermCodeUnitOffset = 0x028;
    internal const int EncodedGlyphDataOffset = 0x038;
    internal const int GlyphsOffset = 0x048;
    [FieldOffset(LoadStateOffset)] internal GrFontRangeLoadState LoadState;
    [FieldOffset(0x008)] internal short PathAllocator08;
    [FieldOffset(0x00A)] internal ushort PathAllocator0A;
    [FieldOffset(RangeAssetKeyOffset)] internal nint RangeAssetKey;
    [FieldOffset(RangeAssetCapacityOffset)] internal uint RangeAssetCapacity18;
    [FieldOffset(RangeAssetCountOffset)] internal uint RangeAssetCount1C;
    [FieldOffset(StartCodeUnitOffset)] internal ushort StartCodeUnit;
    [FieldOffset(LastUse24Offset)] internal uint LastUse24;
    [FieldOffset(TermCodeUnitOffset)] internal ushort TermCodeUnit;
    [FieldOffset(EncodedGlyphDataOffset)] internal nint EncodedGlyphData;
    [FieldOffset(GlyphsOffset)] internal GrFontGlyphHashTable Glyphs;
}
[StructLayout(LayoutKind.Explicit, Size = 0x28)]
internal struct GrFontGlyphHashTable
{
    internal const int SupportedBuild = GrFont.SupportedBuild;
    internal const int NativeSize = 0x28;
    internal const int RecordsOffset = 0x008;
    internal const int RecordCountOffset = 0x014;
    internal const int IndexMaskOffset = 0x020;
    [FieldOffset(RecordsOffset)] internal nint Records;
    [FieldOffset(RecordCountOffset)] internal uint RecordCount;
    [FieldOffset(IndexMaskOffset)] internal uint IndexMask;
}
[StructLayout(LayoutKind.Explicit, Size = 0x34)]
internal struct GrFontGlyphRecord
{
    internal const int SupportedBuild = GrFont.SupportedBuild;
    internal const int NativeSize = 0x34;
    internal const int DataOffsetOffset = 0x000;
    internal const int Metric24Offset = 0x024;
    internal const int RangeIndexOffset = 0x028;
    internal const int CodeUnitOffset = 0x02C;
    internal const int NextIndexOffset = 0x030;
    [FieldOffset(DataOffsetOffset)] internal uint DataOffset;
    [FieldOffset(Metric24Offset)] internal uint Metric24;
    [FieldOffset(RangeIndexOffset)] internal uint RangeIndex;
    [FieldOffset(CodeUnitOffset)] internal ushort CodeUnit;
    [FieldOffset(NextIndexOffset)] internal uint NextIndex;
}
