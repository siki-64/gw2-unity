using System.Runtime.InteropServices;

namespace Gw2.Contracts;

/// <summary>
/// Partial CtlText state for GameBuild.SupportedGameBuild, not FrText storage.
/// Virtual +0x58 copies ColorOverride and invalidates the frame when it changes.
/// A zero alpha selects the text style color at rendering time, not transparent text.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 0xF0)]
internal struct CtlText
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x18)] internal uint FrameId;
    [FieldOffset(0x50)] internal nint OwnerCallback;
    [FieldOffset(0x98)] internal uint ColorOverride;
    [FieldOffset(0xE0)] internal uint TextStyle;
}
