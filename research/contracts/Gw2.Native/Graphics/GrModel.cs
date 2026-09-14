using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0x68)]
internal struct GrModel
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x014)] internal uint Type;
    [FieldOffset(0x030)] internal ulong UserTag;
    [FieldOffset(0x038)] internal uint Unknown38;
    [FieldOffset(0x040)] internal nint CurrentTransformHandle;
    [FieldOffset(0x048)] internal uint Flags48;
    [FieldOffset(0x058)] internal nint Submodels;
    [FieldOffset(0x064)] internal uint SubmodelCount;
}

[StructLayout(LayoutKind.Explicit, Size = 0xE8)]
internal struct GrModelSubmodel
{
    [FieldOffset(0x088)] internal uint PackedColor;
    [FieldOffset(0x090)] internal uint TextureTransformCount;
}
