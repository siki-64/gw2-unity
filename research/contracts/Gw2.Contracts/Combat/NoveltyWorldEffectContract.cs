namespace Gw2.Contracts;

// IN-WORLD ONLY. Novelty-specific authored identities and captured operands.
// Generic SkillDefinition/AvCharEffect/EfCli machinery lives under Gw2.Contracts/Effect.
internal static class NoveltyWorldEffectContract
{
    internal static readonly EffectDefinitionIdentity ChaliceRoot = new(
        0x5B49,
        new EffectContentKey(
            0x481A582882D16B29,
            0x783D69FC72D1B998));

    internal const uint CapturedChaliceMode = 0x11;

    internal static EfCliWorldCreateWrapperArguments CapturedChaliceArguments(
        nint effectDefinition,
        nint characterEffectAgent) =>
        new(
            effectDefinition,
            AvCharEffect.DynamicEffectId,
            CapturedChaliceMode,
            characterEffectAgent,
            0,
            characterEffectAgent,
            AvCharEffect.DefaultScale,
            0);

    internal static bool TryGetRootDefinition(int selection, out EffectDefinitionIdentity identity)
    {
        identity = (NoveltySelection)selection switch
        {
            NoveltySelection.Chalice => ChaliceRoot,
            _ => default,
        };

        return identity.DefinitionId != 0;
    }
}
