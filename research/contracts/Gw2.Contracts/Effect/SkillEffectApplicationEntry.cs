using System.Runtime.InteropServices;

namespace Gw2.Contracts;

// Authored SkillDefinition world-effect application. This is input to
// AvCharEffect and is distinct from the created per-character runtime entry.
[StructLayout(LayoutKind.Explicit, Size = 0x20)]
internal unsafe struct SkillEffectApplicationEntry
{
    [FieldOffset(0x00)] internal IEffectDef* EffectDefinition;

    // 0 = none, 1 = caller primary agent, 2 = owner-derived secondary agent.
    // Native enum name remains unresolved.
    [FieldOffset(0x08)] internal uint SecondaryAgentMode;
    [FieldOffset(0x0C)] internal uint Unknown00C;

    // Consumed as a bitfield after creation. Individual bit names remain unresolved.
    [FieldOffset(0x18)] internal uint Flags;

    [FieldOffset(0x1C)] internal SkillEffectApplicationType Type;
}
