using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0xA8)]
internal struct GdCliContext
{
    [FieldOffset(0x00)] internal nint GdCliContextVtable;
    [FieldOffset(0x08)] internal Unknown008Storage Unknown008;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Unknown008Storage { }
    [FieldOffset(0x10)] internal nint AttackTargetList;
    [FieldOffset(0x18)] internal uint AttackTargetCapacity;
    [FieldOffset(0x1C)] internal uint AttackTargetCount;
    [FieldOffset(0x20)] internal long Unknown20;
    [FieldOffset(0x28)] internal Unknown028Storage Unknown028;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Unknown028Storage { }
    // Sparse array searched by a GdCliContext virtual method.
    [FieldOffset(0x30)] internal nint GadgetsByIndex;
    [FieldOffset(0x38)] internal uint GadgetCapacity;
    [FieldOffset(0x3C)] internal uint GadgetCount;
    [FieldOffset(0x40)] internal long Unknown40;
    [FieldOffset(0x48)] internal int Unknown48;
    [FieldOffset(0x4C)] internal int Unknown4C;
    [FieldOffset(0x50)] internal nint Unknown50;
    [FieldOffset(0x58)] internal nint Unknown58;
    [FieldOffset(0x60)] internal long Unknown60;
    [FieldOffset(0x68)] internal long Unknown68;
    [FieldOffset(0x70)] internal nint Unknown70;
    [FieldOffset(0x78)] internal nint Unknown78;
    [FieldOffset(0x80)] internal nint Unknown80;
    [FieldOffset(0x88)] internal Unknown088Storage Unknown088;
    [StructLayout(LayoutKind.Sequential, Size = 0x20)] internal struct Unknown088Storage { }
}
