using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x88)]
internal unsafe struct ChCliHealth
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal uint HealthUpdateTimestamp;
    [FieldOffset(0x00C)] internal float CurrentHealth;
    [FieldOffset(0x010)] internal float MaximumHealth;
    [FieldOffset(0x014)] internal float HealthRegenRate;
    [FieldOffset(0x018)] internal Unknown018Storage Unknown018;
    [StructLayout(LayoutKind.Sequential, Size = 0x0C)]
    internal struct Unknown018Storage { }
    [FieldOffset(0x024)] internal uint BarrierUpdateTimestamp;
    [FieldOffset(0x028)] internal float Barrier;
    [FieldOffset(0x02C)] internal float MaximumBarrier;
    [FieldOffset(0x030)] internal float BarrierChangeRate;
    [FieldOffset(0x034)] internal Unknown034Storage Unknown034;
    [StructLayout(LayoutKind.Sequential, Size = 0x0C)]
    internal struct Unknown034Storage { }
    [FieldOffset(0x040)] internal float Getter40;
    [FieldOffset(0x044)] internal uint Unknown44;
    [FieldOffset(0x048)] internal ChCliProgress.NotificationList Notifications;
    [FieldOffset(0x078)] internal ChCliCharacter* OwnerCharacter;
    [FieldOffset(0x080)] internal Unknown080Storage Unknown080;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)]
    internal struct Unknown080Storage { }
}
