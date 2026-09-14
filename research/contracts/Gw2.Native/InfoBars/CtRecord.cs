using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit)]
internal struct CtRecord
{
    [FieldOffset(0x00)] internal Unknown00Storage Unknown00;
    [StructLayout(LayoutKind.Sequential, Size = 0xA0)] internal struct Unknown00Storage { }
    [FieldOffset(0xA0)] internal float LagFraction;
    [FieldOffset(0xA4)] internal float HealthFraction;
    [FieldOffset(0xA8)] internal long UnknownA8;
    [FieldOffset(0xB0)] internal float Alpha;
    [FieldOffset(0xB4)] internal float Fraction2;
}
