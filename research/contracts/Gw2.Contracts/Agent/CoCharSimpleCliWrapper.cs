using System.Numerics;
using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct CoCharSimpleCliWrapper
{
    [FieldOffset(0x068)] internal HkpCharacterProxy* CharacterProxy;
    [FieldOffset(0x078)] internal HkpSimpleShapePhantom* PlayerPhysicsPhantom;
    [FieldOffset(0x0B8)] internal Vector3 PositionAlt1;
    [FieldOffset(0x0E8)] internal HkpBoxShape* NpcBoxShape;
    [FieldOffset(0x118)] internal Vector3 PositionAlt2;
}
