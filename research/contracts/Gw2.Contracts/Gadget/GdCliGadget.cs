using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal struct GdCliGadget
{
    [FieldOffset(0x038)] internal nint KeyFramedAgent;
    [FieldOffset(0x208)] internal GadgetType GadgetKind;
    [FieldOffset(0x220)] internal nint Health;
    [FieldOffset(0x4EC)] internal ResourceNodeType ResourceKind;
    [FieldOffset(0x4F0)] internal uint Flags;
}
