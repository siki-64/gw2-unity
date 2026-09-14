using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0xA8)]
internal unsafe struct BtEqpSlot
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal Interface008Storage Interface008;
    [StructLayout(LayoutKind.Sequential, Size = 0x50)] internal struct Interface008Storage { }
    [FieldOffset(0x58)] internal nint Interface58;
    [FieldOffset(0x60)] internal nint Interface60;
    [FieldOffset(0x68)] internal nint PvpAmuletDefinition;
    [FieldOffset(0x70)] internal float Unknown70;
    [FieldOffset(0x74)] internal Unknown074Storage Unknown074;
    [StructLayout(LayoutKind.Sequential, Size = 0x0C)] internal struct Unknown074Storage { }
    [FieldOffset(0x80)] internal int Unknown80;
    [FieldOffset(0x84)] internal uint Unknown84;
    [FieldOffset(0x88)] internal ItemDefinition* ItemDefinition;
    [FieldOffset(0x90)] internal PvpGearProvider* PvpGearProvider;
    [FieldOffset(0x98)] internal PvpGearSlotType SlotType;
    // Sigil slots expose values 0..3, corresponding to PvpWeaponUpgradeIndex;
    // non-sigil slots use a non-index sentinel in the live object.
    [FieldOffset(0x9C)] internal int WeaponUpgradeSlot;
    [FieldOffset(0xA0)] internal int UnknownA0;
    [FieldOffset(0xA4)] internal int UnknownA4;
}
