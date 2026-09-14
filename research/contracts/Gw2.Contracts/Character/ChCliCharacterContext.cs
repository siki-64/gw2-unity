using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x4108)]
internal struct ChCliCharacterContext
{
    [FieldOffset(0x000)] internal nint ChCliCharacterContextVtable;
    [FieldOffset(0x008)] internal nint ChCliCharacterContextInterfaceVtable;
    [FieldOffset(0x010)] internal uint Unknown010;
    [FieldOffset(0x018)] internal nint Unknown018;
    [FieldOffset(0x020)] internal nint Unknown020;
    [FieldOffset(0x028)] internal nint Unknown028;
    [FieldOffset(0x030)] internal nint Unknown030;
    [FieldOffset(0x038)] internal uint Unknown038;
    [FieldOffset(0x040)] internal Unknown040Storage Unknown040;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Unknown040Storage { }
    [FieldOffset(0x048)] internal PvpEquipmentLoadoutArray PvpEquipmentLoadouts;
    [InlineArray(9)]
    internal struct PvpEquipmentLoadoutArray
    {
        internal ChCliItemStorageEquipment Element0;
    }
    [FieldOffset(0x40F8)] internal uint SelectedPvpEquipmentLoadoutIndex;
    [FieldOffset(0x40FC)] internal uint Unknown40FC;
    [FieldOffset(0x4100)] internal uint Unknown4100;
    [FieldOffset(0x4104)] internal Unknown4104Storage Unknown4104;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown4104Storage { }
}
