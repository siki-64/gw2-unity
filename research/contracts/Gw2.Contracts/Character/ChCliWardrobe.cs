using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x708)]
internal struct ChCliWardrobe
{
    // Build-207.032 virtuals used by SbToySlot's world-activation path.
    internal const int GetEquippedToyVtableOffset = 0x250;
    internal const int RequestToySlotActivationVtableOffset = 0x2B8;
    internal const ushort ToySlotActivationOpcode = 0x00F0;

    [FieldOffset(0x000)] internal nint Vtable;

    // Complete client-to-server payload emitted by RequestToySlotActivation.
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x03)]
    internal struct ToySlotActivationMessage
    {
        [FieldOffset(0x00)] internal ushort Opcode;
        [FieldOffset(0x02)] internal byte ToySlot;
    }
}
