using System.Runtime.InteropServices;

namespace Gw2.Contracts;

// Source/assert identity: EfCliEffectDecal.cpp. This mode-2 runtime emits a
// separate map decal after its delay; its lifetime is NOT the map decal's
// visible lifetime. It is not the persistent GpGroundTargeting decal model.
// Only the recovered fields are exposed. No callable creation ABI is implied.
[StructLayout(LayoutKind.Explicit, Size = 0x80)]
internal unsafe struct EfCliEffectDecal
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x18)] internal IEffectDef* Definition;
    [FieldOffset(0x20)] internal uint StateFlags;

    // Three float4 rows copied by the native update routine.
    [FieldOffset(0x34)] internal System.Numerics.Vector4 TransformRow0;
    [FieldOffset(0x44)] internal System.Numerics.Vector4 TransformRow1;
    [FieldOffset(0x54)] internal System.Numerics.Vector4 TransformRow2;
    [FieldOffset(0x64)] internal uint SectionId;
    [FieldOffset(0x78)] internal uint RemainingDelayMilliseconds;
    [FieldOffset(0x7C)] internal float TimeScale;
}
