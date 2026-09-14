using System.Runtime.InteropServices;

namespace Gw2.Contracts;

// Partial authored effect definition. This is distinct from both
// SkillEffectApplicationEntry and the created EfCliEffect runtime object.
[StructLayout(LayoutKind.Explicit, Size = 0x58)]
internal unsafe struct IEffectDef
{
    // The authored object begins with the same 128-bit content key used by the
    // content-definition lookup. Keep the qword aliases for low-level probes.
    [FieldOffset(0x00)] internal EffectContentKey ContentKey;
    [FieldOffset(0x00)] internal ulong ContentKeyLow;
    [FieldOffset(0x08)] internal ulong ContentKeyHigh;
    [FieldOffset(0x10)] internal uint ContentType;
    [FieldOffset(0x14)] internal uint DefinitionId;

    // Native assertion name: effectDef->applicationTargetMode. Operationally
    // this is the 0..11 EfCliEffect factory discriminator.
    [FieldOffset(0x28)] internal EffectApplicationTargetMode ApplicationTargetMode;
    [FieldOffset(0x30)] internal nint ImplementationPayload;
    [FieldOffset(0x38)] internal uint Flags;
    // Model creation clamps each scale component using these bounds.
    // Zero selects the native defaults (0.001 minimum, 100 maximum).
    [FieldOffset(0x40)] internal float MinimumModelScale;
    [FieldOffset(0x44)] internal float MaximumModelScale;
    [FieldOffset(0x48)] internal IEffectDef* AlternateDefinition;

    // Used by the immediate AvCharEffect eligibility guard, not the factory
    // switch. Values 0 and 1 must match the character-side discriminator;
    // value 2 is unrestricted.
    [FieldOffset(0x50)] internal uint CharacterClass;

    internal readonly EffectDefinitionIdentity Identity =>
        new(DefinitionId, ContentKey);
}
