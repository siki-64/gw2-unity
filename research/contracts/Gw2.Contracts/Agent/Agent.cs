using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal struct Agent
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal AgentType Type;
    [FieldOffset(0x0C)] internal uint Id;
    [FieldOffset(0x10)] internal nint Unknown10;
}
