using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0x100)]
internal struct ChCliAdventure
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal Unknown008Storage Unknown008;
    [StructLayout(LayoutKind.Sequential, Size = 0xC8)]
    internal struct Unknown008Storage { }
    [FieldOffset(0x0D0)] internal ChCliProgress.NotificationList Notifications;
}
