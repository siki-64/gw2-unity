using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0xD8)]
internal unsafe struct SkEquipSpecLine
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal Unknown008Storage Unknown008;
    [StructLayout(LayoutKind.Sequential, Size = 0x68)] internal struct Unknown008Storage { }
    [FieldOffset(0x70)] internal nint SpecializationManagerListenerVtable;
    [FieldOffset(0x78)] internal nint TraitManagerListenerVtable;
    [FieldOffset(0x80)] internal Unknown080Storage Unknown080;
    [StructLayout(LayoutKind.Sequential, Size = 0x28)] internal struct Unknown080Storage { }
    [FieldOffset(0xA8)] internal ChCliSpecialization* SpecializationManager;
    [FieldOffset(0xB0)] internal nint BindContext;
    [FieldOffset(0xB8)] internal nint TraitManager;
    [FieldOffset(0xC0)] internal nint UnknownC0;
    [FieldOffset(0xC8)] internal int SpecializationSlot;
    [FieldOffset(0xCC)] internal int UnknownCc;
}
