using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x58)]
internal struct ChCliProgress
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal NotificationList Notifications;
    [FieldOffset(0x038)] internal ushort ProgressByDefinitionAllocatorSize;
    [FieldOffset(0x03A)] internal ushort ProgressByDefinitionAllocatorGeneration;
    [FieldOffset(0x040)] internal nint ProgressByDefinition;
    [FieldOffset(0x048)] internal uint ProgressByDefinitionCount;
    [FieldOffset(0x04C)] internal uint ProgressByDefinitionCapacity;
    [FieldOffset(0x050)] internal nint Unknown050;
    [StructLayout(LayoutKind.Explicit, Size = 0x30)]
    internal struct NotificationList
    {
        [FieldOffset(0x00)] internal uint Unknown000;
        [FieldOffset(0x08)] internal nint Unknown008;
        [FieldOffset(0x10)] internal nint Unknown010;
        [FieldOffset(0x18)] internal nint Unknown018;
        [FieldOffset(0x20)] internal nint Unknown020;
        [FieldOffset(0x28)] internal uint Unknown028;
    }
    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    internal struct ProgressByDefinitionEntry
    {
        [FieldOffset(0x00)] internal uint DefinitionId;
        [FieldOffset(0x08)] internal nint Progress;
    }
}
