using System.Runtime.InteropServices;

namespace Gw2.Native;

// Leading partial FontContext view for build 205.780. The object is much
// larger; only the vtable and the ANTF asset-load gate are promoted. Font
// data, cached-font slots, and the font-key table remain opaque.
[StructLayout(LayoutKind.Explicit, Size = 0x0C)]
internal struct FontContext
{
    // These constants describe the recovered build-205.780 layout only.
    // They are metadata for readers/validators, not a promise that the
    // native object is safe to construct or retain.
    internal const int SupportedBuild = (int)GameBuild.SupportedGameBuild;
    internal const int NativeSize = 0x0C;
    internal const int VtableOffset = 0x000;
    internal const int AssetLoadStateOffset = 0x008;

    // sub_140A68B80 passes this fixed table and count to font registration.
    internal const int CharacterRangeDescriptorRva = 0x01BF8100;
    internal const int CharacterRangeDescriptorCount = 13;

    [FieldOffset(VtableOffset)] internal nint Vtable;

    // sub_140A68B80 enters the ANTF resource load only while this value is
    // zero, then sets it to one after the registration pass.
    [FieldOffset(AssetLoadStateOffset)] internal uint AssetLoadState;
}

// One entry in the fixed build-205.780 FontContext character-range table.
// The static table stores 32-bit half-open bounds; GrFontRange narrows the
// same BMP values to 16-bit fields when a concrete range is constructed.
[StructLayout(LayoutKind.Explicit, Size = 0x08)]
internal struct FontCharacterRangeDescriptor
{
    internal const int NativeSize = 0x08;

    [FieldOffset(0x00)] internal uint StartCodeUnit;
    [FieldOffset(0x04)] internal uint TermCodeUnit;
}

// Descriptive slot names for the fixed thirteen-entry registration table.
// These are reverse-engineering names, not recovered ArenaNet enum symbols.
internal enum FontCharacterRangeSlot
{
    AsciiGraphic = 0,             // [U+0021, U+007F)
    Latin1Supplement = 1,         // [U+00A1, U+00FF)
    LatinExtendedA = 2,            // [U+0100, U+0180)
    GreekAndCyrillic = 3,         // [U+0391, U+0460)
    PunctuationAndSymbols = 4,    // [U+2010, U+266B)
    CjkSymbolsAndPunctuation = 5, // [U+3000, U+3020)
    HiraganaAndKatakana = 6,      // [U+3041, U+3100)
    Bopomofo = 7,                 // [U+3105, U+312A)
    HangulCompatibilityJamo = 8,  // [U+3131, U+318F)
    HangulSyllables = 9,          // [U+AC00, U+D7A4)
    CjkUnifiedIdeographs = 10,    // [U+4E00, U+9FA6)
    CjkCompatibilityIdeographs = 11, // [U+F900, U+FA6B)
    HalfwidthAndFullwidthForms = 12 // [U+FF01, U+FFE7)
}

// Stable descriptive metadata for the fixed build-205.780 registration slots.
// A face may omit any of these slots; this is not the compact GrFontRange list.
internal readonly record struct FontCharacterRangeMetadata(
    FontCharacterRangeSlot Slot,
    ushort StartCodeUnit,
    ushort TermCodeUnit);

internal static class FontCharacterRanges
{
    internal static IReadOnlyList<FontCharacterRangeMetadata> FixedSlots { get; } =
    [
        new(FontCharacterRangeSlot.AsciiGraphic, 0x0021, 0x007F),
        new(FontCharacterRangeSlot.Latin1Supplement, 0x00A1, 0x00FF),
        new(FontCharacterRangeSlot.LatinExtendedA, 0x0100, 0x0180),
        new(FontCharacterRangeSlot.GreekAndCyrillic, 0x0391, 0x0460),
        new(FontCharacterRangeSlot.PunctuationAndSymbols, 0x2010, 0x266B),
        new(FontCharacterRangeSlot.CjkSymbolsAndPunctuation, 0x3000, 0x3020),
        new(FontCharacterRangeSlot.HiraganaAndKatakana, 0x3041, 0x3100),
        new(FontCharacterRangeSlot.Bopomofo, 0x3105, 0x312A),
        new(FontCharacterRangeSlot.HangulCompatibilityJamo, 0x3131, 0x318F),
        new(FontCharacterRangeSlot.HangulSyllables, 0xAC00, 0xD7A4),
        new(FontCharacterRangeSlot.CjkUnifiedIdeographs, 0x4E00, 0x9FA6),
        new(FontCharacterRangeSlot.CjkCompatibilityIdeographs, 0xF900, 0xFA6B),
        new(FontCharacterRangeSlot.HalfwidthAndFullwidthForms, 0xFF01, 0xFFE7)
    ];
}
