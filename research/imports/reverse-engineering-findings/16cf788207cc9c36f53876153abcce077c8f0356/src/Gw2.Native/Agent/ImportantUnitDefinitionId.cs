namespace Gw2.Native;

internal enum ImportantUnitDefinitionId : uint
{
    ChieftainUtahein = 0x20001FE3,
    Svanir = 0x20001BDC,
    BlueLord = 0x200022DE,
    RedLord = 0x200022D1
}

internal static class ImportantUnitDefinitionIds
{
    internal static readonly uint[] All =
        [.. Enum.GetValues<ImportantUnitDefinitionId>().Select(static value => (uint)value)];
}
