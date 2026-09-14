using System.Numerics;
using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0x130)]
internal struct FrameContentParams
{
    [FieldOffset(0x00)] internal int OrderingKey;
    [FieldOffset(0x04)] internal int Layer;
    [FieldOffset(0x08)] internal nint Material;
    [FieldOffset(0x10)] internal ulong MaterialData;
    [FieldOffset(0x18)] internal uint PackedColor;
    [FieldOffset(0x1C)] internal ModelRect Rect;
    [FieldOffset(0x2C)] internal float Unknown2C;
    [FieldOffset(0x30)] internal Vector2 TransformOrigin;
    [FieldOffset(0x38)] internal int DrawFlag;
    [FieldOffset(0x3C)] internal Vector4 ShaderControlInput;
    // Their exact authored meaning remains unresolved.
    [FieldOffset(0x4C)] internal Vector2 TextureCoordinateInput0;
    [FieldOffset(0x54)] internal Vector2 TextureCoordinateInput1;
    [FieldOffset(0x5C)] internal Vector2 TextureCoordinateInput2;
    [FieldOffset(0x64)] internal Vector2 TextureCoordinateInput3;
    [FieldOffset(0x6C)] internal TextureCoordinateTransformStorage TextureCoordinateTransforms;
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    internal struct ModelRect
    {
        internal float X0;
        internal float Y0;
        internal float X1;
        internal float Y1;
    }
    [StructLayout(LayoutKind.Sequential, Size = 0xC4)]
    internal struct TextureCoordinateTransformStorage { }
}
