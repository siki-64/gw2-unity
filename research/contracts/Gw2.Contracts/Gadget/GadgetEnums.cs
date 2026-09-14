namespace Gw2.Contracts;

internal enum GadgetType : uint
{
    Destructible = 1,
    Point = 2,
    Generic = 3,
    Generic2 = 4,
    Crafting = 5,
    Door = 6,
    BountyBoard = 11,
    Interact = 12,
    Rift = 13,
    PlayerSpecific = 14,
    AttackTarget = 16,
    MapPortal = 17,
    Waypoint = 18,
    ResourceNode = 19,
    Prop = 20,
    PlayerCreated = 23,
    Vista = 24,
    BuildSite = 25,
    None = 26
}

internal enum ResourceNodeType : int
{
    Plant = 0,
    Tree = 1,
    Rock = 2,
    Quest = 3,
    None = 4,
    Cache = 6
}

internal enum AttackTargetCombatState : int
{
    Idle = 2,
    InCombat = 3
}
