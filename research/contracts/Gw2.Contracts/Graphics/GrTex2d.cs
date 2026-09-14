using System.Runtime.InteropServices;

namespace Gw2.Contracts;

// Partial native 2D texture object. The renderer/backend handle mapping is
// deliberately not modeled because its current interpretation is not stable.
[StructLayout(LayoutKind.Explicit, Size = 0x80)]
internal struct GrTex2d
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x048)] internal uint Width;
    [FieldOffset(0x04C)] internal uint Height;
    [FieldOffset(0x070)] internal uint HandleCount;
    // Native read-only texture reference/handle field. The concrete pointee or
    // handle type remains unresolved.
    [FieldOffset(0x078)] internal nint ReadOnlyTexture;
}
