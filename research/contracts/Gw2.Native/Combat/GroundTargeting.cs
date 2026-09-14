using System.Runtime.InteropServices;

namespace Gw2.Native;

// GpGroundTargeting's interface at complete-object +0x18, as received by the
// skill-arm/update routines. These offsets must not be applied to the complete
// object. See re/notes/World/Effects/skill-outlines.md.
[StructLayout(LayoutKind.Explicit, Size = 0x74)]
internal unsafe struct GroundTargeting
{
    [FieldOffset(0x008)] internal nint CollisionShape;
    [FieldOffset(0x040)] internal nint DecalModel;
    [FieldOffset(0x048)] internal nint PreviewModel;
    [FieldOffset(0x050)] internal nint RotateModel;
    [FieldOffset(0x058)] internal SkillDefinition* ArmedSkill;
    [FieldOffset(0x060)] internal uint ArmedSlot;
    [FieldOffset(0x070)] internal uint Flags070;
}
