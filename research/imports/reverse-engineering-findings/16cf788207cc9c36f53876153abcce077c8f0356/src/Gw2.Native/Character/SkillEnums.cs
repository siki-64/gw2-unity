namespace Gw2.Native;

internal enum ChCliConfiguredSkillSlot : byte
{
    HealSkill = 0,
    UtilitySkill1 = 1,
    UtilitySkill2 = 2,
    UtilitySkill3 = 3,
    EliteSkill = 4,
    Count = 5,
    // F2-F4 spectator entries currently arrive without a usable slot field.
    ProfessionMechanic = 21
}

internal enum SkillContentId : uint
{
    None = 0
}

[Flags]
internal enum SkillDefinitionFlag : uint
{
    None = 0,
    // Skills carrying this bit use no range in the recovered native checks.
    NoRange = 0x1000
}

internal enum ChCliSkillContextCode : byte
{
    ConfiguredSkillsA0 = 0,
    ConfiguredSkillsB1 = 1,
    ConfiguredSkillsA2 = 2,
    ConfiguredSkillsB3 = 3
}

internal enum ChCliSkillMessageId : ushort
{
    // Per-player configured standard-skill assignment.
    ConfiguredSkillUpdate = 0x264,
    // Current-character runtime combat skillbar update.
    RuntimeSkillbarSlotUpdate = 0x27C
}
