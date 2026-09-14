using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal struct AgKeyFramed
{
    [FieldOffset(0x008)] internal AgentType Type;
    [FieldOffset(0x00C)] internal int Id;
    [FieldOffset(0x040)] internal GadgetType GadgetKind;
    [FieldOffset(0x050)] internal nint CoordinateSystem;
    [FieldOffset(0x0B8)] internal nint CharacterAgent;
}
