namespace Gw2.Native;

internal enum ItemRarity : int
{
    None = -1,
    Junk = 0,
    Common = 1,
    Fine = 2,
    Masterwork = 3,
    Rare = 4,
    Exotic = 5,
    Ascended = 6,
    Legendary = 7,
    End = 8
}

internal enum ItemLocation : int
{
    None = 0,
    Agent = 1,
    Equipment = 2,
    Inventory = 3,
    InventoryAccount = 4,
    InventoryBagSlot = 5,
    InventoryOverflow = 6,
    Lootable = 7,
    Vendor = 8,
    InventoryShared = 10,
    InventoryArmory = 11,
    InventoryLegendaryArmory = 12,
    Count = 13
}

internal enum EquipmentSlot : int
{
    None = -1,
    AquaticHelm = 0,
    Back = 1,
    Chest = 2,
    Boots = 3,
    Gloves = 4,
    Helm = 5,
    Pants = 6,
    Shoulders = 7,
    OutfitChest = 8,
    OutfitBoots = 9,
    OutfitGloves = 10,
    OutfitHelm = 11,
    OutfitPants = 12,
    OutfitShoulders = 13,
    TownChest = 14,
    TownBoots = 15,
    TownGloves = 16,
    TownHelm = 17,
    TownPants = 18,
    Accessory1 = 19,
    Accessory2 = 20,
    Ring1 = 21,
    Ring2 = 22,
    Amulet = 23,
    AquaticWeapon1 = 24,
    AquaticWeapon2 = 25,
    Novelty = 26,
    TransformWeapon = 27,
    TransformWeaponOffhand = 28,
    MainhandWeapon1 = 29,
    OffhandWeapon1 = 30,
    MainhandWeapon2 = 31,
    OffhandWeapon2 = 32,
    Toy = 33,
    ForagingTool = 34,
    LoggingTool = 35,
    MiningTool = 36,
    PvpAquaticHelm = 40,
    PvpBack = 41,
    PvpChest = 42,
    PvpBoots = 43,
    PvpGloves = 44,
    PvpHelm = 45,
    PvpPants = 46,
    PvpShoulders = 47,
    PvpAquaticWeapon1 = 48,
    PvpAquaticWeapon2 = 49,
    PvpMainhandWeapon1 = 50,
    PvpOffhandWeapon1 = 51,
    PvpMainhandWeapon2 = 52,
    PvpOffhandWeapon2 = 53,
    // Legacy PvP Equipment.
    PvpAccessory1 = 54,
    PvpAccessory2 = 55,
    PvpRing1 = 56,
    PvpRing2 = 57,
    PvpAmulet = 58,
    PvpJewel = 59,
    FishingRod = 60,
    FishingLure = 61,
    FishingBait = 62,
    JadeBotPowerCore = 63,
    JadeBotSkin = 64,
    JadeBotSensoryArray = 65,
    JadeBotServiceChip = 66,
    Relic = 67,
    Backpack1 = 68,
    End = 69
}

internal enum PvpWeaponUpgradeIndex : int
{
    WeaponSet1First = 0,
    WeaponSet2First = 1,
    WeaponSet1Second = 2,
    WeaponSet2Second = 3
}

internal enum PvpGearSlotType : int
{
    Amulet = 0,
    Relic = 1,
    Rune = 2,
    Sigil = 3
}

internal static class EquipmentSlotExtensions
{
    internal static bool IsWeapon(this EquipmentSlot slot) => slot is
        EquipmentSlot.MainhandWeapon1 or
        EquipmentSlot.OffhandWeapon1 or
        EquipmentSlot.MainhandWeapon2 or
        EquipmentSlot.OffhandWeapon2;
}

internal enum WeaponType : int
{
    None = -1,
    Sword = 0,
    Hammer = 1,
    Longbow = 2,
    Shortbow = 3,
    Axe = 4,
    Dagger = 5,
    Greatsword = 6,
    Mace = 7,
    Pistol = 8,
    Rifle = 10,
    Scepter = 11,
    Staff = 12,
    Focus = 13,
    Torch = 14,
    Warhorn = 15,
    Shield = 16,
    End = 23
}
