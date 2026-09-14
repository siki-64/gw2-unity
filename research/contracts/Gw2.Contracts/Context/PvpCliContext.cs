using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal struct PvpCliContext
{
    [FieldOffset(0x000)] internal nint PvpCliContextVtable;
    [FieldOffset(0x008)] internal Unknown008Storage Unknown008;
    [StructLayout(LayoutKind.Sequential, Size = 0x270)] internal struct Unknown008Storage { }
    [FieldOffset(0x278)] internal PvpCliContextQueryInterface QueryInterface;
}
[StructLayout(LayoutKind.Explicit, Size = 0x8)]
internal struct PvpCliContextQueryInterface
{
    [FieldOffset(0x000)] internal nint PvpCliContextQueryInterfaceVtable;
}
