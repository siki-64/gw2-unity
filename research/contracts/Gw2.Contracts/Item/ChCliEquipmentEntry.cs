using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct ChCliEquipmentEntry
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal nint Unknown08Vtable;
    [FieldOffset(0x10)] internal Unknown010Storage Unknown010;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)] internal struct Unknown010Storage { }
    [FieldOffset(0x40)] internal ItemDefinition* ItemDefinition;
    [FieldOffset(0x48)] internal ushort LocationAndFlags;
    [FieldOffset(0x4A)] internal Unknown04AStorage Unknown04A;
    [StructLayout(LayoutKind.Sequential, Size = 0x0E)] internal struct Unknown04AStorage { }
    // Polymorphic by ItemLocation (agent, inventory, equipment, lootable, vendor, ...).
    [FieldOffset(0x58)] internal nint LocationData;
    [FieldOffset(0x60)] internal Unknown060Storage Unknown060;
    [StructLayout(LayoutKind.Sequential, Size = 0x40)] internal struct Unknown060Storage { }
    [FieldOffset(0xA0)] internal ItemStatDefinition* GearStatDefinition;
    [FieldOffset(0xA8)] internal ItemStatDefinition* WeaponStatDefinition;
}
