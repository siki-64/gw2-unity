using System.Runtime.InteropServices;

namespace Gw2.Native;

// Tag 0x630, slot 7. Native class/visual identity remains unresolved.
// Constructor/handler 0x1403BDD00; agent setter 0x1403BE220 in the analyzed image.
[StructLayout(LayoutKind.Explicit, Size = 0x78)]
internal unsafe struct InfoBarSlot7Widget
{
    [FieldOffset(0x00)] internal nint AgentInterface;
    [FieldOffset(0x08)] internal nint Interface08;
    [FieldOffset(0x10)] internal nint Interface10;
    [FieldOffset(0x18)] internal uint FrameId;
    [FieldOffset(0x1C)] internal uint Unknown1C;
    [FieldOffset(0x20)] internal Unknown20Storage Unknown20;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)] internal struct Unknown20Storage { }
    // Initial creation payload supplies InfoBar*; message 0x35 can replace this.
    [FieldOffset(0x50)] internal nint CallbackContext;
    [FieldOffset(0x58)] internal Agent* Agent;
    [FieldOffset(0x60)] internal nint AgentNext;
    [FieldOffset(0x68)] internal nint AgentPrevious;
    // Zero selects 8x8 geometry; nonzero selects 16x16 and another draw datum.
    [FieldOffset(0x70)] internal int LargeGeometry;
    [FieldOffset(0x74)] internal uint Unknown74;
}
