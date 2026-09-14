using System.Numerics;
using System.Runtime.InteropServices;

namespace Gw2.Contracts;

// Partial read-only overlay for the model setup descriptor (fourth argument).
// Reads are proven through +0x4B; 0x50 is the aligned covered span, not a proven
// allocation size. This is not a constructor-ready activation payload.
[StructLayout(LayoutKind.Explicit, Size = 0x50)]
internal struct EfCliModelPlacement
{
    // 0: listener source; 1: explicit position/rotation; 2: context position;
    // 3: additional binding/range values; 4: no mode-specific copy in setup.
    [FieldOffset(0x00)] internal uint Mode;
    [FieldOffset(0x08)] internal ulong Value08;
    [FieldOffset(0x10)] internal ulong Value10;
    [FieldOffset(0x18)] internal ulong Value18;
    [FieldOffset(0x20)] internal Vector3 Position;
    // Three angles consumed by the native trigonometric matrix builder.
    // Axis order is deliberately unnamed.
    [FieldOffset(0x2C)] internal Vector3 RotationAngles;
    [FieldOffset(0x38)] internal uint Value38;
    [FieldOffset(0x3C)] internal float Value3C;
    [FieldOffset(0x40)] internal float Value40;
    [FieldOffset(0x44)] internal float Value44;
    [FieldOffset(0x48)] internal float Value48;
}
