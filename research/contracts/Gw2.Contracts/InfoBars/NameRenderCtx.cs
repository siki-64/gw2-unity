using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x110)]
internal struct NameRenderCtx
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal nint Interface08;
    [FieldOffset(0x10)] internal nint Interface10;
    [FieldOffset(0x18)] internal uint FrameId;
    [FieldOffset(0x1C)] internal uint Unknown1C;
    [FieldOffset(0x20)] internal Unknown20Storage Unknown20;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)] internal struct Unknown20Storage { }
    [FieldOffset(0x50)] internal nint Unknown50;
    [FieldOffset(0x58)] internal nint CharacterTrackedClient;
    [FieldOffset(0x60)] internal nint Interface60;
    [FieldOffset(0x68)] internal nint Interface68;
    [FieldOffset(0x70)] internal nint Interface70;
    [FieldOffset(0x78)] internal nint Interface78;
    [FieldOffset(0x80)] internal nint Interface80;
    [FieldOffset(0x88)] internal nint GadgetTrackedClient;
    [FieldOffset(0x90)] internal nint Interface90;
    [FieldOffset(0x98)] internal nint GuildListener;
    [FieldOffset(0xA0)] internal nint NameCategoryListener;
    [FieldOffset(0xA8)] internal nint Agent;
    [FieldOffset(0xB0)] internal nint AgentNext;
    [FieldOffset(0xB8)] internal nint AgentPrevious;
    [FieldOffset(0xC0)] internal NameCategory Category;
    [FieldOffset(0xC4)] internal int UnknownC4;
    [FieldOffset(0xC8)] internal int DisplayLevel;
    [FieldOffset(0xCC)] internal int UnknownCC;
    [FieldOffset(0xD0)] internal int UnknownD0;
    [FieldOffset(0xD4)] internal int UnknownD4;
    [FieldOffset(0xD8)] internal nint Guild;
    [FieldOffset(0xE0)] internal nint MarkerContext;
    [FieldOffset(0xE8)] internal nint MasterCharacter;
    [FieldOffset(0xF0)] internal nint UnknownF0;
    [FieldOffset(0xF8)] internal nint RegisteredPlayer;
    [FieldOffset(0x100)] internal nint Unknown100;
    [FieldOffset(0x108)] internal nint RegisteredTransformationManager;
}
