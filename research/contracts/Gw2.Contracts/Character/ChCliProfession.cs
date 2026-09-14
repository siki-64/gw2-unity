using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x48)]
internal unsafe struct ChCliProfession
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal ChCliProgress.NotificationList Notifications;
    [FieldOffset(0x038)] internal ChCliCharacter* OwnerCharacter;
    [FieldOffset(0x040)] internal CharacterProfessionState ProfessionState;
    [FieldOffset(0x044)] internal uint Unknown044;
}
