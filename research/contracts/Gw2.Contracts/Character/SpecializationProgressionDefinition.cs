using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct SpecializationProgressionDefinition
{
    [FieldOffset(0x000)] internal Unknown000Storage Unknown000;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)] internal struct Unknown000Storage { }
    [FieldOffset(0x030)] internal SpecializationProgressionThreshold* Thresholds;
    [FieldOffset(0x038)] internal uint ThresholdCount;
}

[StructLayout(LayoutKind.Explicit, Size = 0x30)]
internal struct SpecializationProgressionThreshold
{
    [FieldOffset(0x000)] internal uint RequiredProgress;
    [FieldOffset(0x004)] internal Unknown004Storage Unknown004;
    [StructLayout(LayoutKind.Sequential, Size = 0x2C)] internal struct Unknown004Storage { }
}
