using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0x298)]
internal unsafe struct ChCliSkillbar
{
    internal const int SkillSlotCount = 23;
    internal const uint NoActiveSkillSlot = 0x17;
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x074)] internal uint ActiveSkillSlot;
    [FieldOffset(0x080)] internal AsContext* AsContext;
    [FieldOffset(0x0B0)] internal uint UsingSkill;
    [FieldOffset(0x0F0)] internal ChCliCharacter* OwnerCharacter;
    [FieldOffset(0x100)] internal nint Notifier;
    [FieldOffset(0x150)] internal ChCliSkillRecharge* Recharge;
    [FieldOffset(0x158)] internal nint RegisteredCombatant;
    [FieldOffset(0x160)] internal ChCliCoreStats* RegisteredCoreStats;
    [FieldOffset(0x168)] internal ChCliInventory* RegisteredInventory;
    [FieldOffset(0x178)] internal nint RegisteredTransformationManager;
    [FieldOffset(0x180)] internal uint HeldInstantSkillSlot;
    [FieldOffset(0x1D0)] internal SkillSlotDefinitionArray SkillSlotDefinitions;
    [InlineArray(SkillSlotCount)]
    internal struct SkillSlotDefinitionArray
    {
        internal nint Element0;
    }
}
