using System.Runtime.InteropServices;

namespace Gw2.Contracts;

// Partial toy definition used by ChCliWardrobe and EqpPageToy.cpp. This object
// is the shared semantic ancestor of wardrobe toy selection and hero-panel
// preview. Its preview sequence is still strictly PREVIEW-PANEL authored data;
// it is not a SkillDefinition or an in-world Buff application array.
[StructLayout(LayoutKind.Explicit)]
internal struct ToyDefinition
{
    internal const uint ContentClass = 0x019A;

    // ChCliWardrobe.cpp reads this value and submits it in the separate 0x00EF
    // toy-definition selection request. CnContext also resolves content class
    // 0x019A to ToyDefinition objects by numeric id.
    [FieldOffset(0x28)] internal uint DefinitionId;

    [FieldOffset(0x70)] internal nint PreviewEffects;
    [FieldOffset(0x78)] internal uint PreviewEffectCount;

    // Consumed by the paper-doll setup around RVA 0x5D2740.
    [FieldOffset(0x80)] internal float PreviewScale;
    [FieldOffset(0x84)] internal float PreviewAngleDegrees;
    [FieldOffset(0x88)] internal float PreviewParameter88;
}

// EqpPageToy preview sequence element, stride 0x18. RVA 0x5D2940 passes
// EffectDefinition directly to the CPaperDoll PlayEffect virtual. NextUpdate
// is converted from milliseconds to seconds by the preview scheduler, and
// AngleDegrees is converted to radians before PlayEffect.
[StructLayout(LayoutKind.Explicit, Size = 0x18)]
internal struct ToyPreviewEffectEntry
{
    [FieldOffset(0x00)] internal nint EffectDefinition;
    [FieldOffset(0x08)] internal float ScaleMultiplier;
    [FieldOffset(0x0C)] internal float NextUpdateMilliseconds;
    [FieldOffset(0x10)] internal float AngleDegrees;
}
