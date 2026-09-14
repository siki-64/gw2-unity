using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0x490)]
internal unsafe struct ChCliInventory
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal nint Unknown08Vtable;
    [FieldOffset(0x010)] internal Unknown010Storage Unknown010;
    [StructLayout(LayoutKind.Sequential, Size = 0x150)] internal struct Unknown010Storage { }
    [FieldOffset(0x160)] internal EquipmentSlotArray EquipmentSlots;
    [StructLayout(LayoutKind.Sequential, Size = 0x220)]
    internal struct EquipmentSlotArray
    {
        internal ChCliEquipmentEntry* Element0;
    }
    [FieldOffset(0x380)] internal BagSlotArray BagSlots;
    [InlineArray(24)]
    internal struct BagSlotArray
    {
        internal nint Element0;
    }
    [FieldOffset(0x440)] internal uint BagCount;
    [FieldOffset(0x444)] internal uint Unknown444;
    [FieldOffset(0x448)] internal Unknown448Storage Unknown448;
    [StructLayout(LayoutKind.Sequential, Size = 0x48)] internal struct Unknown448Storage { }
}
