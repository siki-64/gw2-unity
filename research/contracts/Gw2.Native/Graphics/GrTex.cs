using System.Runtime.InteropServices;

namespace Gw2.Native;

// Partial graphics texture wrapper. Only the independently reconstructed
// leading fields are modeled.
[StructLayout(LayoutKind.Explicit, Size = 0x28)]
internal unsafe struct GrTex
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal uint RefCount;
    [FieldOffset(0x010)] internal GrTex2d* Owner;
    [FieldOffset(0x018)] internal nint Callback;
    [FieldOffset(0x020)] internal nint UserData;
}
