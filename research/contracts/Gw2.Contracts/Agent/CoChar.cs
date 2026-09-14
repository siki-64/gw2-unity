using System.Numerics;
using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct CoChar
{
    [FieldOffset(0x030)] internal Vector3 VisualPosition;
    [FieldOffset(0x060)] internal HkpRigidBody* PlayerRigidBody;
    [FieldOffset(0x088)] internal CoCharSimpleCliWrapper* SimpleCliWrapper;
    [FieldOffset(0x100)] internal HkpSimpleShapePhantom* PlayerPhysicsPhantom;
    [FieldOffset(0x110)] internal Vector2 NpcCurrentDirection;
    [FieldOffset(0x150)] internal float VelocityX;
    [FieldOffset(0x154)] internal float VelocityY;
    [FieldOffset(0x158)] internal float VerticalVelocity;
    [FieldOffset(0x170)] internal HkpBoxShape* NpcBoxShape;
    [FieldOffset(0x180)] internal Vector2 PlayerCurrentDirection;
    [FieldOffset(0x188)] internal float PlayerLookAngleVertical;
    [FieldOffset(0x294)] internal float Gravity;
    [FieldOffset(0x29C)] internal float MaxClimbSlope;
    [FieldOffset(0x2B8)] internal float MovementSpeed;
}
