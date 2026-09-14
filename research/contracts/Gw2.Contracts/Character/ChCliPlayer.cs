using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0xA178)]
internal unsafe struct ChCliPlayer
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal Unknown008Storage Unknown008;
    [StructLayout(LayoutKind.Sequential, Size = 0x10)] internal struct Unknown008Storage { }
    [FieldOffset(0x018)] internal ChCliCharacter* Character;
    [FieldOffset(0x020)] internal ChCliCharacter* Character20;
    [FieldOffset(0x028)] internal Unknown028Storage Unknown028;
    [StructLayout(LayoutKind.Sequential, Size = 0x38)] internal struct Unknown028Storage { }
    [FieldOffset(0x060)] internal ushort Unknown060;
    [FieldOffset(0x062)] internal ushort Unknown062;
    [FieldOffset(0x068)] internal nint Name;
    [FieldOffset(0x070)] internal uint Unknown070;
    [FieldOffset(0x074)] internal uint PlayerListIndex;
    [FieldOffset(0x078)] internal Unknown078Storage Unknown078;
    [StructLayout(LayoutKind.Sequential, Size = 0x4EA0)] internal struct Unknown078Storage { }
    [FieldOffset(0x4F18)] internal ChCliCharacterContext PvpCharacterContext;
    [FieldOffset(0x9020)] internal Unknown9020Storage Unknown9020;
    [StructLayout(LayoutKind.Sequential, Size = 0x60)] internal struct Unknown9020Storage { }
    [FieldOffset(0x9080)] internal Unknown9080Storage Unknown9080;
    [StructLayout(LayoutKind.Sequential, Size = 0x6D8)] internal struct Unknown9080Storage { }
    [FieldOffset(0x9758)] internal ChCliProgress Progress;
    [FieldOffset(0x97B0)] internal PvpGearProvider* PvpGearManager;
    [FieldOffset(0x97B8)] internal Unknown97B8Storage Unknown97B8;
    [StructLayout(LayoutKind.Sequential, Size = 0x2A0)] internal struct Unknown97B8Storage { }
    [FieldOffset(0x9A58)] internal nint RewardTrackManager;
    [FieldOffset(0x9A60)] internal Unknown9A60Storage Unknown9A60;
    [StructLayout(LayoutKind.Sequential, Size = 0x178)] internal struct Unknown9A60Storage { }
    [FieldOffset(0x9BD8)] internal ChCliSkill Skill;
    [FieldOffset(0x9CB8)] internal ChCliSpecialization Specialization;
    [FieldOffset(0x9D78)] internal Unknown9D78Storage Unknown9D78;
    [StructLayout(LayoutKind.Sequential, Size = 0x400)] internal struct Unknown9D78Storage { }
}
