using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct HkpRigidBody
{
    [FieldOffset(0x010)] internal HkpWorld* World;
    [FieldOffset(0x020)] internal HkpShape* Shape;
    [FieldOffset(0x04C)] internal HkcdShapeType WrapperShapeType;
    [FieldOffset(0x150)] internal nint Motion;
}
[StructLayout(LayoutKind.Explicit)]
internal struct HkpSimpleShapePhantom
{
    [FieldOffset(0x110)] internal float CollisionOffsetX;
    [FieldOffset(0x120)] internal Vector3 PhysicsPosition;
}
[StructLayout(LayoutKind.Explicit)]
internal struct HkpCharacterProxy
{
    [FieldOffset(0x028)] internal HkCharacterGroundState GroundState;
}
[StructLayout(LayoutKind.Explicit)]
internal unsafe struct HkpWorld
{
    [FieldOffset(0x188)] internal HkpBroadPhaseBorder* BroadPhaseBorder;
}
[StructLayout(LayoutKind.Explicit)]
internal struct HkpBroadPhaseBorder
{
    [FieldOffset(0x000)] internal PhantomPointerArray Phantoms;
    [InlineArray(6)]
    internal struct PhantomPointerArray
    {
        internal nint Element0;
    }
}
[StructLayout(LayoutKind.Explicit)]
internal struct HkpAabbPhantom
{
    [FieldOffset(0x0F0)] internal Vector4 AabbMin;
    [FieldOffset(0x100)] internal Vector4 AabbMax;
}
