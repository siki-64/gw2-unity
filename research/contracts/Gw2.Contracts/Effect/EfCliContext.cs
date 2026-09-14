using System.Runtime.InteropServices;

namespace Gw2.Contracts;

// Build-205.780 client effect runtime context. Unresolved container internals
// deliberately remain opaque.
[StructLayout(LayoutKind.Explicit, Size = Size)]
internal struct EfCliContext
{
    internal const int Size = 0x1A8;
    internal const int SingletonRva = 0x28AA230;
    internal const int ConstructorRva = 0x1343CF0;
    internal const int DestructorRva = 0x1343FD0;
    internal const int ScalarDeletingDestructorRva = 0x1344270;
    internal const int AccessorRva = 0x1345BD0;
    internal const int TickRva = 0x13448E0;
    internal const int CreateCoreRva = 0x1344C50;
    internal const int CreateWrapperRva = 0x1344B70;
    internal const int RemoveRva = 0x1345AD0;
    internal const int StopRva = 0x1345B50;

    internal const int TickVtableOffset = 0x00;
    internal const int CreateCoreVtableOffset = 0x08;
    internal const int CreateWrapperVtableOffset = 0x10;
    internal const int RemoveVtableOffset = 0x30;
    internal const int StopVtableOffset = 0x38;
    internal const int DefinitionPreloadVtableOffset = 0x58;
    internal const int DefinitionSubstitutionVtableOffset = 0xC0;
    internal const int RuntimeHandleBindingVtableOffset = 0xC8;
    internal const int ScalarDeletingDestructorVtableOffset = 0xD0;

    internal const int DefinitionRegistryOffset = 0x18;
    internal const int ActiveEffectsOffset = 0x80;
    internal const int ActiveEffectHeadOffset = 0x90;
    internal const int DeferredRemovalQueueOffset = 0xA8;
    internal const int OwnedHelperOffset = 0x110;
    internal const int UpdateStateFlagsOffset = 0x170;
    internal const int RuntimeModeOffset = 0x174;

    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal nint SecondaryVtable;
    [FieldOffset(0x010)] internal nint TertiaryVtable;
    [FieldOffset(ActiveEffectHeadOffset)] internal nint ActiveEffectHead;
    [FieldOffset(OwnedHelperOffset)] internal nint OwnedHelper;
    [FieldOffset(UpdateStateFlagsOffset)] internal uint UpdateStateFlags;
    [FieldOffset(RuntimeModeOffset)] internal uint RuntimeMode;
}

// Observed common prefix through the intrusive-list link. Concrete factory
// subclasses are larger (0x80 through 0x210).
[StructLayout(LayoutKind.Explicit, Size = 0x78)]
internal unsafe struct EfCliEffect
{
    internal const int GracefulTeardownVtableOffset = 0x168;
    internal const int ImmediateStopVtableOffset = 0x170;
    internal const int CleanupVtableOffset = 0x1E0;
    internal const uint TeardownRequestedFlag = 0x02;
    internal const uint ImmediateRemovalFlag = 0x10;
    internal const uint CompletionFlag = 0x20;

    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x18)] internal IEffectDef* Definition;
    [FieldOffset(0x20)] internal uint StateFlags;
    [FieldOffset(0x30)] internal uint TrackedId;
    // Common placement, distinct from the model-family matrix at +0x120.
    // Section-tagged positions require native world-section conversion.
    [FieldOffset(0x34)] internal System.Numerics.Vector4 TransformRow0;
    [FieldOffset(0x44)] internal System.Numerics.Vector4 TransformRow1;
    [FieldOffset(0x54)] internal System.Numerics.Vector4 TransformRow2;
    [FieldOffset(0x64)] internal uint SectionId;
    [FieldOffset(0x68)] internal nint ActiveListPrevious;
    // Bit zero marks an end sentinel; otherwise this is the next object.
    [FieldOffset(0x70)] internal nint ActiveListNext;

    internal readonly System.Numerics.Vector3 Position =>
        new(TransformRow0.W, TransformRow1.W, TransformRow2.W);
}
