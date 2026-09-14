namespace Gw2.Contracts;

internal readonly record struct EfCliWorldCreateWrapperArguments(
    nint EffectDefinition,
    uint EffectId,
    uint Mode,
    nint PrimaryAgent,
    nint SelectedSecondary,
    nint AuxiliaryAgent,
    float Scale,
    uint TrailingFlags);

internal readonly record struct EfCliWorldCreateCoreArguments(
    nint EffectDefinition,
    uint EffectId,
    uint Mode,
    nint PrimaryAgent,
    nint SelectedSecondary,
    nint AuxiliaryAgent,
    uint DerivedAgentFlags,
    float Scale,
    uint TrailingFlags);
