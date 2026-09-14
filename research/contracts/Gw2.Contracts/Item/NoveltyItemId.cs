namespace Gw2.Contracts;

internal enum NoveltySelection
{
    BurningSwords,
    Dragon,
    Storm,
    Comet,
    Chalice,
    Skull,
    Crown,
    Sun,
    Moon,
    MadKing,
    Laurels,
    WinterCrown,
    Count
}

// Monthly Automated Tournament first-place novelty item ids. Each entry has a
// regular reward item and the account-unlock/vendor representation exposed by
// the game data as a second API item id.
internal enum NoveltyItemId : uint
{
    BurningSwords = 86759,
    BurningSwordsAccount = 90716,
    Dragon = 86918,
    DragonAccount = 90643,
    Storm = 87130,
    StormAccount = 90880,
    Comet = 87464,
    CometAccount = 90609,
    Chalice = 87541,
    ChaliceAccount = 90368,
    Skull = 87568,
    SkullAccount = 90850,
    Crown = 81604,
    CrownAccount = 90747,
    Sun = 82447,
    SunAccount = 90872,
    Moon = 84260,
    MoonAccount = 90353,
    MadKing = 85504,
    MadKingAccount = 90788,
    Laurels = 85434,
    LaurelsAccount = 90732,
    WinterCrown = 86560,
    WinterCrownAccount = 90656
}
