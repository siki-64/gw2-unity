using System.Runtime.InteropServices;

namespace Gw2.Native;

// Build 205780: FrApi child allocation is 0x2E0. See re/notes/UI/Frames/frapi.md.
[StructLayout(LayoutKind.Explicit, Size = 0x2E0)]
internal struct FrFrame
{
    [FieldOffset(0x000)] internal nint PendingLink;
    [FieldOffset(0x008)] internal nuint PendingLinkTagged;
    [FieldOffset(0x060)] internal nint ParentChainLink;
    [FieldOffset(0x030)] internal float ScreenX0;
    [FieldOffset(0x034)] internal float ScreenY0;
    [FieldOffset(0x038)] internal float ScreenX1;
    [FieldOffset(0x03C)] internal float ScreenY1;
    [FieldOffset(0x108)] internal FrFrameContentQueue ContentQueue;
    [FieldOffset(0x270)] internal uint FrameId;
    [FieldOffset(0x29C)] internal FrFrameState State;
    [FieldOffset(0x2A0)] internal uint CreationFlags;
}

[Flags]
internal enum FrFrameState : uint
{
    Created = 0x4,
    Destroying = 0x8,
    Hidden = 0x200
}
