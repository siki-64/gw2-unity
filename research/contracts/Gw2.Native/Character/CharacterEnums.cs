namespace Gw2.Native;

internal enum Profession : uint
{
    None = 0,
    Guardian = 1,
    Warrior = 2,
    Engineer = 3,
    Ranger = 4,
    Thief = 5,
    Elementalist = 6,
    Mesmer = 7,
    Necromancer = 8,
    Revenant = 9,
    End = 10
}


internal enum Race : byte
{
    Asura = 0,
    Charr = 1,
    Human = 2,
    Norn = 3,
    Sylvari = 4,
    None = 5
}

internal enum CharacterRank : int
{
    Normal = 0,
    Ambient = 1,
    Veteran = 2,
    Elite = 3,
    Champion = 4,
    Legendary = 5,
    End = 6
}

[Flags]
internal enum CharacterRankFlag : uint
{
    None = 0,
    Champion = 1 << 1,
    Elite = 1 << 5,
    Legendary = 1 << 11,
    Ambient = 1 << 23,
    Veteran = 1 << 29
}

[Flags]
internal enum ChCliCharacterFlag : ulong
{
    None = 0,
    // Native FLAG_CREATE_FINALIZED.
    CreateFinalized = 1UL << 4,
    // Native FLAG_TASK_MGR_HAS_REGISTERED.
    TaskManagerHasRegistered = 1UL << 21,
    // Native FLAG_SKILL_MGR_HAS_REGISTERED.
    SkillManagerHasRegistered = 1UL << 22,
    // Native FLAG_RENOWN_NPC_SUBREGION_ACTIVATED.
    RenownNpcSubregionActivated = 1UL << 23,
    // Native FLAG_PROGRESS_MGR_HAS_REGISTERED.
    ProgressManagerHasRegistered = 1UL << 41
}

internal enum Attitude : uint
{
    Friendly = 0,
    Hostile = 1,
    Indifferent = 2,
    Neutral = 3
}

internal enum CharacterWaterState : uint
{
    Dry = 0,
    Diving = 1,
    Surface = 2
}

internal enum CharacterProfessionState : uint
{
    None = 0,
    AttunementFire = 1,
    AttunementWater = 2,
    AttunementAir = 3,
    AttunementEarth = 4,
    AttunementFirst = AttunementFire,
    AttunementLast = AttunementEarth,

    GuardianLuminaryShroud = 6,
    NecromancerShroud = 7,
    ThiefSpecterShroud = 8,

    RevenantLegendGlint = 0x0F,
    RevenantLegendShiro = 0x10,
    RevenantLegendJalis = 0x11,
    RevenantLegendMallyx = 0x12,
    RevenantLegendKalla = 0x13,
    RevenantLegendVentari = 0x14,
    RevenantLegendAlliance = 0x15,
    RevenantLegendRazah = 0x16,

    RevenantLegendFirst = RevenantLegendGlint,
    RevenantLegendLast = RevenantLegendRazah,
    Count = 0x17
}
