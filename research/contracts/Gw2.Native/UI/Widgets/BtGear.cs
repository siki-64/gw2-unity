using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0xE0)]
internal unsafe struct BtGear
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal Interface008Storage Interface008;
    [StructLayout(LayoutKind.Sequential, Size = 0x40)] internal struct Interface008Storage { }
    [FieldOffset(0x48)] internal nint SourceInventory;
    [FieldOffset(0x50)] internal Interface050Storage Interface050;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Interface050Storage { }
    [FieldOffset(0x58)] internal nint Interface58;
    [FieldOffset(0x60)] internal nint Interface60;
    [FieldOffset(0x68)] internal nint Interface68;
    [FieldOffset(0x70)] internal nint Interface70;
    [FieldOffset(0x78)] internal nint Interface78;
    [FieldOffset(0x80)] internal nint Interface80;
    [FieldOffset(0x88)] internal nint Interface88;
    [FieldOffset(0x90)] internal Unknown090Storage Unknown090;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)] internal struct Unknown090Storage { }
    // UI-selection state consumed when updating equipment-slot visibility.
    [FieldOffset(0xC0)] internal nint SelectedSlotWidget;
    [FieldOffset(0xC8)] internal Unknown0C8Storage Unknown0C8;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Unknown0C8Storage { }
    [FieldOffset(0xD0)] internal nint EquipmentManager;
    // This retains the spectated ChCliPlayer.
    [FieldOffset(0xD8)] internal ChCliPlayer* Player;
}
