using System.Numerics;
using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct CoKeyFramed
{
    [FieldOffset(0x030)] internal Vector3 Position;
    [FieldOffset(0x060)] internal HkpRigidBody* RigidBody;
    [FieldOffset(0x0F8)] internal Vector2 Rotation;
}
