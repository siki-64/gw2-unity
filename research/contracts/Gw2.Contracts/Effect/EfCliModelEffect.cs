using System.Runtime.InteropServices;

namespace Gw2.Contracts;

// Build-207.032 common runtime object allocated for applicationTargetMode
// 4, 6, and 10. Only fields established by constructor/setup code are exposed.
// The 3x4 transform at +0x120 is row-major; its fourth column is the world
// translation written by the model-effect setup path.
[StructLayout(LayoutKind.Explicit, Size = Size)]
internal unsafe struct EfCliModelEffect
{
    internal const int Size = 0x210;
    internal const int ConstructorRva = 0x134FD40;
    internal const int SetupRva = 0x1352840;
    internal const int SetupCommonReturnRva = 0x1352A78;

    internal const int TransformOffset = 0x120;
    internal const int PositionXOffset = 0x12C;
    internal const int PositionYOffset = 0x13C;
    internal const int PositionZOffset = 0x14C;
    internal const int AttachmentReferenceOffset = 0x198;

    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x18)] internal IEffectDef* Definition;
    [FieldOffset(0x20)] internal uint StateFlags;
    [FieldOffset(0x30)] internal uint TrackedId;

    [FieldOffset(0x78)] internal nint PlacementListenerVtable;
    [FieldOffset(0xD0)] internal nint AuthoredModelBinding;
    [FieldOffset(0xD8)] internal nint Mode6Payload68;
    [FieldOffset(0xE0)] internal ulong PlacementValue10;
    [FieldOffset(0xE8)] internal float ModelScale;
    // Result of the native model creation routine, not the retained setup host.
    [FieldOffset(0xF8)] internal nint ModelObject;
    [FieldOffset(0x100)] internal nint ModelCreationPayload;
    [FieldOffset(0x108)] internal uint SetupParameter;
    [FieldOffset(0x10C)] internal uint ModelStateFlags;
    [FieldOffset(0x110)] internal float SetupScale;
    [FieldOffset(0x118)] internal ulong PlacementValue08;

    [FieldOffset(0x120)] internal float Transform00;
    [FieldOffset(0x124)] internal float Transform01;
    [FieldOffset(0x128)] internal float Transform02;
    [FieldOffset(PositionXOffset)] internal float PositionX;
    [FieldOffset(0x130)] internal float Transform10;
    [FieldOffset(0x134)] internal float Transform11;
    [FieldOffset(0x138)] internal float Transform12;
    [FieldOffset(PositionYOffset)] internal float PositionY;
    [FieldOffset(0x140)] internal float Transform20;
    [FieldOffset(0x144)] internal float Transform21;
    [FieldOffset(0x148)] internal float Transform22;
    [FieldOffset(PositionZOffset)] internal float PositionZ;

    [FieldOffset(0x150)] internal uint PlacementMode;
    [FieldOffset(0x154)] internal uint ModelBindingId;
    [FieldOffset(0x158)] internal ulong PlacementValue18;
    [FieldOffset(0x160)] internal uint PlacementValue38;
    [FieldOffset(0x164)] internal float PlacementValue3C;
    [FieldOffset(0x168)] internal float PlacementValue40;
    [FieldOffset(0x16C)] internal float PlacementValue44;
    [FieldOffset(0x170)] internal float PlacementValue48;
    // Setup retains through host virtual +0x08; cleanup releases through +0x00.
    [FieldOffset(0x178)] internal nint HostObject;
    // Mode 0 registers this object's +0x78 listener through source virtual +0x20.
    [FieldOffset(0x180)] internal nint PlacementSource;
    [FieldOffset(0x18C)] internal float TimeScale;
    [FieldOffset(0x190)] internal int EffectId;

    // Intrusive tracked reference, distinct from HostObject's retention protocol.
    [FieldOffset(AttachmentReferenceOffset)] internal nint AttachmentObject;
    [FieldOffset(0x1A0)] internal nint AttachmentNext;
    [FieldOffset(0x1A8)] internal nint AttachmentPrevious;
}
