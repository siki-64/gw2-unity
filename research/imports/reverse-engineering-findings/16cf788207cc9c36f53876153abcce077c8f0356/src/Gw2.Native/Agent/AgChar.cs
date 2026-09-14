using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit)]
internal struct AgChar
{
    [FieldOffset(0x008)] internal AgentType Type;
    [FieldOffset(0x00C)] internal int Id;
    [FieldOffset(0x050)] internal nint CoordinateSystem;
    [FieldOffset(0x120)] internal System.Numerics.Vector3 GroundedPosition32;
    [FieldOffset(0x12C)] internal uint GroundedTimestamp;
    [FieldOffset(0x1B8)] internal byte MovementState;
}
