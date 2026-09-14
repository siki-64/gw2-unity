namespace Gw2.Contracts;

// Stable authored effect definition ids from native content data. Use the full
// EffectDefinitionIdentity when distinguishing authored effects at runtime.
internal enum EffectDefinitionId : uint
{
    ThrowMine = 24005,
    ThrowMineGadgeteer = 22886,
    Aed = 7401,
    RenewedFocus = 3559,
    EffulgentStance = 19681
}

internal static class EffectDefinitionIdentities
{
    internal static readonly EffectDefinitionIdentity ThrowMine = new(
        (uint)EffectDefinitionId.ThrowMine,
        new EffectContentKey(0x4B42BD56866BE22E, 0x06AAB04CEAA7F99B));

    internal static readonly EffectDefinitionIdentity ThrowMineGadgeteer = new(
        (uint)EffectDefinitionId.ThrowMineGadgeteer,
        new EffectContentKey(0x4FC0BCD7803EBFB6, 0xDCF9ACC748DD0596));

    internal static readonly EffectDefinitionIdentity Aed = new(
        (uint)EffectDefinitionId.Aed,
        new EffectContentKey(0x4C29D6C029458DA2, 0x87C82B01FCC68CA8));

    internal static readonly EffectDefinitionIdentity RenewedFocus = new(
        (uint)EffectDefinitionId.RenewedFocus,
        new EffectContentKey(0x44E6A78F13D64D9F, 0xEFBAD3E3F9277083));

    internal static readonly EffectDefinitionIdentity EffulgentStance = new(
        (uint)EffectDefinitionId.EffulgentStance,
        new EffectContentKey(0x4AA16F226EA7F3B6, 0x4558E85DC3E24DA5));
}
