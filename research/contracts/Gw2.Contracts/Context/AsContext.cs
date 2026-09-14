using System.Numerics;
using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x3E0)]
internal struct AsContext
{
    [FieldOffset(0x000)] internal nint AsContextVtable;
    [FieldOffset(0x008)] internal Unknown008Storage Unknown008;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)] internal struct Unknown008Storage { }
    [FieldOffset(0x038)] internal nint ActionChaseSelection;
    [FieldOffset(0x040)] internal Unknown040Storage Unknown040;
    [StructLayout(LayoutKind.Sequential, Size = 0x18)] internal struct Unknown040Storage { }
    [FieldOffset(0x058)] internal Unknown058Storage Unknown058;
    [StructLayout(LayoutKind.Sequential, Size = 0x18)] internal struct Unknown058Storage { }
    [FieldOffset(0x070)] internal nint AutoSelection;
    [FieldOffset(0x078)] internal Unknown078Storage Unknown078;
    [StructLayout(LayoutKind.Sequential, Size = 0x18)] internal struct Unknown078Storage { }
    [FieldOffset(0x090)] internal uint ContextMode;
    [FieldOffset(0x094)] internal Unknown094Storage Unknown094;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown094Storage { }
    [FieldOffset(0x098)] internal nint Slot98;
    [FieldOffset(0x0A0)] internal Unknown0A0Storage Unknown0A0;
    [StructLayout(LayoutKind.Sequential, Size = 0x90)] internal struct Unknown0A0Storage { }
    [FieldOffset(0x130)] internal TrackedUnitSlot HoverSelection;
    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    internal struct TrackedUnitSlot
    {
        [FieldOffset(0x00)] internal nint Value;
        [FieldOffset(0x08)] internal nint Next;
        [FieldOffset(0x10)] internal nint Previous;
    }
    [FieldOffset(0x148)] internal long Unknown148;
    [FieldOffset(0x150)] internal TrackedUnitSlot InteractionSelectionExposed;
    [FieldOffset(0x168)] internal TrackedUnitSlot InteractionSelectionRaw;
    [FieldOffset(0x180)] internal long Unknown180;
    [FieldOffset(0x188)] internal TrackedUnitSlot Unknown188;
    [FieldOffset(0x1A0)] internal TrackedUnitSlot PrimaryPicked;
    [FieldOffset(0x1B8)] internal TrackedUnitSlot PersonalTarget;
    [FieldOffset(0x1D0)] internal TrackedUnitSlot Slot1D0;
    [FieldOffset(0x1E8)] internal TrackedUnitSlot MouseoverMirror;
    [FieldOffset(0x200)] internal TrackedUnitSlot Unknown200;
    [FieldOffset(0x218)] internal TrackedUnitSlot Selection;
    [FieldOffset(0x230)] internal TrackedUnitSlot Unknown230;
    [FieldOffset(0x248)] internal long Unknown248;
    [FieldOffset(0x250)] internal nint CallTarget;
    [FieldOffset(0x258)] internal Unknown258Storage Unknown258;
    [StructLayout(LayoutKind.Sequential, Size = 0x40)] internal struct Unknown258Storage { }
    [FieldOffset(0x298)] internal TrackedUnitSlot LockedSelection;
    [FieldOffset(0x2B0)] internal long Unknown2B0;
    [FieldOffset(0x2B8)] internal nint LockedSpectateSelection;
    [FieldOffset(0x2C0)] internal Unknown2C0Storage Unknown2C0;
    [StructLayout(LayoutKind.Sequential, Size = 0x18)] internal struct Unknown2C0Storage { }
    [FieldOffset(0x2D8)] internal Vector3 CursorRayDirection;
    [FieldOffset(0x2E4)] internal Unknown2E4Storage Unknown2E4;
    [StructLayout(LayoutKind.Sequential, Size = 0x1C)] internal struct Unknown2E4Storage { }
    [FieldOffset(0x300)] internal Vector3 CursorRayHitPosition;
    [FieldOffset(0x30C)] internal int Unknown30C;
    [FieldOffset(0x310)] internal Vector3 CursorRayOrigin;
    [FieldOffset(0x31C)] internal Unknown31CStorage Unknown31C;
    [StructLayout(LayoutKind.Sequential, Size = 0x1C)] internal struct Unknown31CStorage { }
    // Overlaps the independently recovered registry view below.
    [FieldOffset(0x338)] internal ChCliProgress.NotificationList Notifications;
    [FieldOffset(0x340)] internal nint TrackedList;
    [FieldOffset(0x348)] internal long Unknown348;
    [FieldOffset(0x350)] internal long Unknown350;
    [FieldOffset(0x358)] internal long Unknown358;
    [FieldOffset(0x360)] internal long Unknown360;
    [FieldOffset(0x368)] internal nint SkillTargetSelection;
    [FieldOffset(0x370)] internal long Unknown370;
    [FieldOffset(0x378)] internal long Unknown378;
    [FieldOffset(0x380)] internal long Unknown380;
    [FieldOffset(0x388)] internal nint RegisteredCharacterContext;
    [FieldOffset(0x390)] internal nint RegisteredAgentView;
    [FieldOffset(0x398)] internal long Unknown398;
    [FieldOffset(0x3A0)] internal nint RegisteredControlledCharacter;
    [FieldOffset(0x3A8)] internal nint Slot3A8;
    [FieldOffset(0x3B0)] internal Unknown3B0Storage Unknown3B0;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Unknown3B0Storage { }
    [FieldOffset(0x3B8)] internal nint RegisteredTransformationManager;
    [FieldOffset(0x3C0)] internal nint RegisteredInputBinding;
    [FieldOffset(0x3C8)] internal nint LifetimeRegistrationOwner;
    [FieldOffset(0x3D0)] internal nint LifetimeRegistrationPrevious;
    [FieldOffset(0x3D8)] internal nint LifetimeRegistrationNext;
}
