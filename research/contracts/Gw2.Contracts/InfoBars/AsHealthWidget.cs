using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[Flags]
internal enum AsHealthConfigTag : uint
{
    ThickHealthbarGeometry = 0x800,
}
[StructLayout(LayoutKind.Explicit, Size = 0x78)]
internal unsafe struct AsHealthWidget
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal uint FrameId;
    [FieldOffset(0x0C)] internal uint Unknown0C;
    [FieldOffset(0x10)] internal Unknown10Storage Unknown10;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)] internal struct Unknown10Storage { }
    [FieldOffset(0x40)] internal nint CombatTrackerListener;
    [FieldOffset(0x48)] internal nint NameCategoryListener;
    [FieldOffset(0x50)] internal nint PolicyListener;
    [FieldOffset(0x58)] internal Agent* Agent;
    [FieldOffset(0x60)] internal nint AgentNext;
    [FieldOffset(0x68)] internal nint AgentPrevious;
    [FieldOffset(0x70)] internal int CombatTrackerRegistrationActive;
    [FieldOffset(0x74)] internal int EmitterSpaceFlag;
}
[StructLayout(LayoutKind.Explicit, Size = 0x284)]
internal struct AsHealthFrameView
{
    [FieldOffset(0x00)] internal Unknown00Storage Unknown00;
    [StructLayout(LayoutKind.Sequential, Size = 0x27C)] internal struct Unknown00Storage { }
    [FieldOffset(0x27C)] internal WidgetStateFlag StateFlags;
    [FieldOffset(0x280)] internal AsHealthConfigTag ConfigTag;
}
