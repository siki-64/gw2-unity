using System.Numerics;
using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal struct HkpShape
{
    [FieldOffset(0x010)] internal HkcdShapeType ShapeType;
}
[StructLayout(LayoutKind.Explicit)]
internal struct HkpBoxShape
{
    [FieldOffset(0x010)] internal HkcdShapeType ShapeType;
    [FieldOffset(0x020)] internal float CollisionRadius;
    [FieldOffset(0x030)] internal Vector4 HalfExtents;
}
[StructLayout(LayoutKind.Explicit)]
internal struct HkpCylinderShape
{
    [FieldOffset(0x010)] internal HkcdShapeType ShapeType;
    [FieldOffset(0x028)] internal float Radius;
    [FieldOffset(0x02C)] internal float HalfHeight;
}
[StructLayout(LayoutKind.Explicit)]
internal unsafe struct HkpMoppBvTreeShape
{
    [FieldOffset(0x010)] internal HkcdShapeType ShapeType;
    [FieldOffset(0x028)] internal nint Code;
    [FieldOffset(0x058)] internal HkpShape* ChildShape;
}
[StructLayout(LayoutKind.Explicit)]
internal struct HkpExtendedMeshShape
{
    [FieldOffset(0x010)] internal HkcdShapeType ShapeType;
    [FieldOffset(0x0C0)] internal Vector4 AabbHalfExtents;
}
[StructLayout(LayoutKind.Explicit)]
internal struct HkpListShape
{
    [FieldOffset(0x010)] internal HkcdShapeType ShapeType;
    [FieldOffset(0x050)] internal Vector4 BoundingBoxHalfExtents;
    [FieldOffset(0x068)] internal float Unknown068;
}
