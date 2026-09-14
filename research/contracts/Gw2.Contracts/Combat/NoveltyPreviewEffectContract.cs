namespace Gw2.Contracts;

// PREVIEW-PANEL ONLY. These definitions belong to wardrobe/toybox preview
// rendering and are not valid substitutes for equipped/in-world novelty effects.
internal static class NoveltyPreviewEffectContract
{
    internal static readonly EffectDefinitionIdentity LaurelsRoot = new(
        0xB2D1,
        new EffectContentKey(
            0x4000CF6BFF88B749,
            0x36D3EB0E2199988A));

    internal static bool TryGetRootDefinition(int selection, out EffectDefinitionIdentity identity)
    {
        identity = (NoveltySelection)selection switch
        {
            NoveltySelection.Laurels => LaurelsRoot,
            _ => default,
        };

        return identity.DefinitionId != 0;
    }
}
