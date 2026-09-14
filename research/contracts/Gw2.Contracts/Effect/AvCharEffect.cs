using System.Runtime.InteropServices;

namespace Gw2.Contracts;

// Reconstructed receive-side character-effect object. This type contains only native object/data
// relationships; executable addresses and callsites are not modelled here.
[StructLayout(LayoutKind.Explicit)]
internal unsafe struct AvCharEffect
{
    internal const uint EffectStateBit08 = 0x08;
    internal const uint EffectStateBit04 = 0x04;
    internal const uint DynamicEffectId = uint.MaxValue;
    internal const uint ModeBaseOne = 0x01;
    internal const uint ModeBaseThree = 0x03;
    internal const uint ModeModifier = 0x10;
    internal const float DefaultScale = 1.0f;

    [FieldOffset(0x078)] internal RuntimeEntryArray RuntimeEntries;
    [FieldOffset(0x098)] internal PairedApplicationBindingArray PairedApplicationBindings;
    [FieldOffset(0x230)] internal StowedBuffEffectArray StowedBuffEffects;
    [FieldOffset(0x3E0)] internal uint StateFlags;

    internal static bool IsLiveEffectState(uint stateFlags) =>
        (stateFlags & EffectStateBit08) != 0 &&
        (stateFlags & EffectStateBit04) == 0;

    internal static bool IsPairedApplicationType(SkillEffectApplicationType type) =>
        type is SkillEffectApplicationType.PairedFalse or SkillEffectApplicationType.PairedTrue;

    internal static bool IsNormalBuffWorldApplicationType(SkillEffectApplicationType type) =>
        type == SkillEffectApplicationType.Standard || IsPairedApplicationType(type);

    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    internal struct RuntimeEntryArray
    {
        [FieldOffset(0x00)] internal ushort AllocatorId;
        [FieldOffset(0x02)] internal ushort AllocatorGeneration;
        [FieldOffset(0x04)] internal uint Unknown04;
        [FieldOffset(0x08)] internal RuntimeEntry* Data;
        [FieldOffset(0x10)] internal uint Capacity;
        [FieldOffset(0x14)] internal uint Count;
        [FieldOffset(0x18)] internal uint Growth;
        [FieldOffset(0x1C)] internal uint Unknown1C;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    internal struct PairedApplicationBindingArray
    {
        [FieldOffset(0x00)] internal ushort AllocatorId;
        [FieldOffset(0x02)] internal ushort AllocatorGeneration;
        [FieldOffset(0x04)] internal uint Unknown04;
        [FieldOffset(0x08)] internal PairedApplicationBindingEntry* Data;
        [FieldOffset(0x10)] internal uint Capacity;
        [FieldOffset(0x14)] internal uint Count;
        [FieldOffset(0x18)] internal uint Growth;
        [FieldOffset(0x1C)] internal uint Unknown1C;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    internal struct StowedBuffEffectArray
    {
        [FieldOffset(0x00)] internal ushort AllocatorId;
        [FieldOffset(0x02)] internal ushort AllocatorGeneration;
        [FieldOffset(0x04)] internal uint Unknown04;
        [FieldOffset(0x08)] internal StowedBuffEffectEntry* Data;
        [FieldOffset(0x10)] internal uint Capacity;
        [FieldOffset(0x14)] internal uint Count;
        [FieldOffset(0x18)] internal uint Growth;
        [FieldOffset(0x1C)] internal uint Unknown1C;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    internal struct RuntimeEntry
    {
        [FieldOffset(0x00)] internal uint SkillId;
        [FieldOffset(0x04)] internal uint RuntimeHandle;
        [FieldOffset(0x08)] internal nint EffectObject;
        [FieldOffset(0x10)] internal nint EffectControl;
        [FieldOffset(0x18)] internal nint EffectReserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    internal struct PairedApplicationBindingEntry
    {
        [FieldOffset(0x00)] internal uint SkillId;
        [FieldOffset(0x08)] internal SkillEffectApplicationEntry* ApplicationEntry;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    internal struct StowedBuffEffectEntry
    {
        [FieldOffset(0x00)] internal uint SkillId;
        [FieldOffset(0x08)] internal IEffectDef* EffectDefinition;
        [FieldOffset(0x10)] internal SkillEffectApplicationEntry* ApplicationEntry;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x18)]
    internal struct EffectOwnershipWrapper
    {
        internal nint Effect;
        internal nint Control;
        internal nint Reserved;
    }
}
