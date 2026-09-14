namespace Gw2.Contracts;

// Native IEffectDef::applicationTargetMode. Exact ArenaNet semantic names are
// unresolved, so numeric names are intentional. Modes 4/6/10 share the model
// runtime family and modes 1/11 share the compound runtime family. Mode 2 is
// EfCliEffectDecal (confirmed by constructor/vtable/source-assert linkage).
internal enum EffectApplicationTargetMode : uint
{
    Mode0 = 0,
    Mode1 = 1,
    Mode2 = 2,
    Mode3 = 3,
    Mode4 = 4,
    Mode5 = 5,
    Mode6 = 6,
    Mode7 = 7,
    Mode8 = 8,
    Mode9 = 9,
    Mode10 = 10,
    Mode11 = 11,
}

internal enum SkillEffectApplicationType : uint
{
    Standard = 0x00000001,
    Auxiliary = 0x00000004,
    PairedTrue = 0x00200000,
    PairedFalse = 0x00400000,
}

internal static class EffectApplicationTargetModeExtensions
{
    internal static bool IsModelFamily(this EffectApplicationTargetMode mode) =>
        mode is EffectApplicationTargetMode.Mode4 or
            EffectApplicationTargetMode.Mode6 or
            EffectApplicationTargetMode.Mode10;

    internal static bool IsCompoundFamily(this EffectApplicationTargetMode mode) =>
        mode is EffectApplicationTargetMode.Mode1 or EffectApplicationTargetMode.Mode11;
}
