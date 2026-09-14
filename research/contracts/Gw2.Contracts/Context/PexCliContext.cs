using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct PexCliContext
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x0D0)] internal PexCliMatch* Match;
    [FieldOffset(0x110)] internal PvpQueueObject* Queue;
}

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct PexCliMatch
{
    internal const uint AcceptanceState = 4;

    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x0C0)] internal uint State;
    [FieldOffset(0x150)] internal PlCliChannel* PortalChannel;
}

[StructLayout(LayoutKind.Explicit)]
internal struct PvpQueueObject
{
    internal const uint QueuedFlag = 1u << 3;

    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x1B0)] internal uint Flags;
}
