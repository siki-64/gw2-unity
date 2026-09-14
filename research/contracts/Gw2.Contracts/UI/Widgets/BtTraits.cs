using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0xA0)]
internal unsafe struct BtTraits
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal Interface008Storage Interface008;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)] internal struct Interface008Storage { }
    [FieldOffset(0x38)] internal nint TraitManager;
    [FieldOffset(0x40)] internal Interface040Storage Interface040;
    [StructLayout(LayoutKind.Sequential, Size = 0x18)] internal struct Interface040Storage { }
    [FieldOffset(0x58)] internal nint Interface58;
    [FieldOffset(0x60)] internal nint MeterProviderListenerVtable;
    [FieldOffset(0x68)] internal Unknown068Storage Unknown068;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)] internal struct Unknown068Storage { }
    [FieldOffset(0x98)] internal ChCliSpecialization* SpecializationManager;
}
