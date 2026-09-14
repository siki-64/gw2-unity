namespace Gw2.Contracts;

internal enum HkCharacterGroundState : int
{
    InAir = 0,
    OnGround = 1,
    HillyGround = 2
}

internal enum HkcdShapeType : byte
{
    Cylinder = 0x01,
    Box = 0x03,
    List = 0x08,
    Mopp = 0x09,
    ExtendedMesh = 0x0D
}
