using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x58)]
internal struct ChCliChatter
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal nint InterfaceVtable08;
    [FieldOffset(0x10)] internal nint InterfaceVtable10;
    [FieldOffset(0x18)] internal nint InterfaceVtable18;
    [FieldOffset(0x20)] internal nint InterfaceVtable20;
    [FieldOffset(0x28)] internal nint InterfaceVtable28;
    [FieldOffset(0x30)] internal nint InterfaceVtable30;
    [FieldOffset(0x38)] internal nint InterfaceVtable38;
    [FieldOffset(0x40)] internal nint InterfaceVtable40;
    [FieldOffset(0x48)] internal nint InterfaceVtable48;
    [FieldOffset(0x50)] internal uint Unknown50;
}
