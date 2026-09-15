using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x548)]
internal unsafe struct ChCliContext
{
    [FieldOffset(0x000)] internal nint ChCliContextVtable;
    [FieldOffset(0x008)] internal Unknown008Storage Unknown008;
    [StructLayout(LayoutKind.Explicit, Size = 0x30)]
    internal struct Unknown008Storage
    {
        [FieldOffset(0x00)] internal nint InterfaceVtable08;
        [FieldOffset(0x08)] internal nint InterfaceVtable10;
        [FieldOffset(0x10)] internal nint InterfaceVtable18;
        [FieldOffset(0x18)] internal nint InterfaceVtable20;
        [FieldOffset(0x20)] internal nint InterfaceVtable28;
        [FieldOffset(0x28)] internal nint InterfaceVtable30;
    }
    [FieldOffset(0x038)] internal NativeArrayHeader Unknown038;
    [FieldOffset(0x058)] internal ushort CharacterAllocatorId;
    [FieldOffset(0x05A)] internal ushort CharacterAllocatorGeneration;
    [FieldOffset(0x05C)] internal uint Unknown05C;
    [FieldOffset(0x060)] internal ChCliCharacter** Characters;
    [FieldOffset(0x068)] internal uint CharacterCapacity;
    [FieldOffset(0x06C)] internal uint CharacterCount;
    [FieldOffset(0x070)] internal uint CharacterGrowth;
    [FieldOffset(0x074)] internal uint Unknown074;
    [FieldOffset(0x078)] internal ushort PlayerAllocatorId;
    [FieldOffset(0x07A)] internal ushort PlayerAllocatorGeneration;
    [FieldOffset(0x07C)] internal uint Unknown07C;
    [FieldOffset(0x080)] internal ChCliPlayer** Players;
    [FieldOffset(0x088)] internal uint PlayerCapacity;
    [FieldOffset(0x08C)] internal uint PlayerCount;
    [FieldOffset(0x090)] internal uint PlayerGrowth;
    [FieldOffset(0x094)] internal uint Unknown094;
    [FieldOffset(0x098)] internal ChCliCharacter* LocalCharacter;
    [FieldOffset(0x0A0)] internal Unknown0A0Storage Unknown0A0;
    [StructLayout(LayoutKind.Sequential, Size = 0x270)]
    internal struct Unknown0A0Storage { }
    [FieldOffset(0x310)] internal ProfessionLookupArray ProfessionLookup;
    [InlineArray(11)]
    internal struct ProfessionLookupArray
    {
        internal nint Element0;
    }

    [FieldOffset(0x368)] internal RaceLookupArray RaceLookup;
    [InlineArray(5)]
    internal struct RaceLookupArray
    {
        internal nint Element0;
    }

    [FieldOffset(0x390)] internal Unknown390Storage Unknown390;
    [StructLayout(LayoutKind.Sequential, Size = 0x160)]
    internal struct Unknown390Storage { }
    [FieldOffset(0x4F0)] internal ushort AttitudePresenceAllocatorId;
    [FieldOffset(0x4F2)] internal ushort AttitudePresenceAllocatorGeneration;
    [FieldOffset(0x4F4)] internal uint Unknown4F4;
    [FieldOffset(0x4F8)] internal uint* AttitudePresenceWords;
    [FieldOffset(0x500)] internal uint AttitudePresenceCapacity;
    [FieldOffset(0x504)] internal uint AttitudePresenceWordCount;
    [FieldOffset(0x508)] internal uint AttitudePresenceGrowth;
    [FieldOffset(0x50C)] internal uint Unknown50C;
    [FieldOffset(0x510)] internal ushort AttitudeAllocatorId;
    [FieldOffset(0x512)] internal ushort AttitudeAllocatorGeneration;
    [FieldOffset(0x514)] internal uint Unknown514;
    [FieldOffset(0x518)] internal byte* Attitudes;
    [FieldOffset(0x520)] internal uint AttitudeCapacity;
    [FieldOffset(0x524)] internal uint AttitudeCount;
    [FieldOffset(0x528)] internal uint AttitudeGrowth;
    [FieldOffset(0x52C)] internal uint Unknown52C;
    [FieldOffset(0x530)] internal nint Unknown530;
    [FieldOffset(0x538)] internal nint Unknown538;
    [FieldOffset(0x540)] internal uint Unknown540;
    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    internal struct NativeArrayHeader
    {
        [FieldOffset(0x00)] internal ushort AllocatorId;
        [FieldOffset(0x02)] internal ushort AllocatorGeneration;
        [FieldOffset(0x04)] internal uint Unknown04;
        [FieldOffset(0x08)] internal nint Data;
        [FieldOffset(0x10)] internal uint Capacity;
        [FieldOffset(0x14)] internal uint Count;
        [FieldOffset(0x18)] internal uint Growth;
        [FieldOffset(0x1C)] internal uint Unknown1C;
    }
}
