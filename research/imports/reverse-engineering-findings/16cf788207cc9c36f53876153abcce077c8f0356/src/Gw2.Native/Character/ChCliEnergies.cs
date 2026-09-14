using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0x110)]
internal struct ChCliEnergies
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal EnergyRecordArray EnergyRecords;
    [InlineArray(4)]
    internal struct EnergyRecordArray
    {
        private EnergyRecord element0;
    }
    [FieldOffset(0x078)] internal Unknown078Storage Unknown078;
    [StructLayout(LayoutKind.Sequential, Size = 0x60)] internal struct Unknown078Storage { }
    [FieldOffset(0x0D8)] internal Unknown0D8Storage Unknown0D8;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Unknown0D8Storage { }
    [FieldOffset(0x0E0)] internal nint Unknown0E0;
    [FieldOffset(0x0E8)] internal nint Unknown0E8;
    [FieldOffset(0x0F0)] internal nint Unknown0F0;
    [FieldOffset(0x0F8)] internal nint Unknown0F8;
    [FieldOffset(0x100)] internal Unknown100Storage Unknown100;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Unknown100Storage { }
    [FieldOffset(0x108)] internal nint Unknown108;
    [StructLayout(LayoutKind.Explicit, Size = 0x1C)]
    internal struct EnergyRecord
    {
        [FieldOffset(0x00)] internal uint Unknown000;
        [FieldOffset(0x04)] internal float Unknown004;
        [FieldOffset(0x08)] internal float Unknown008;
        [FieldOffset(0x0C)] internal float Unknown00C;
        [FieldOffset(0x10)] internal float Unknown010;
        [FieldOffset(0x14)] internal float Unknown014;
        [FieldOffset(0x18)] internal uint Unknown018;
    }
}
