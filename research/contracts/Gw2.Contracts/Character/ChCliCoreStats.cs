using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x288)]
internal unsafe struct ChCliCoreStats
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal Unknown008Storage Unknown008;
    [StructLayout(LayoutKind.Explicit, Size = 0x2B)]
    internal struct Unknown008Storage
    {
        [FieldOffset(0x000)] internal nint InterfaceVtable;
    }
    [FieldOffset(0x033)] internal Race Race;
    [FieldOffset(0x034)] internal byte AppearanceScale;
    [FieldOffset(0x035)] internal byte Unknown035;
    [FieldOffset(0x036)] internal Unknown036Storage Unknown036;
    [StructLayout(LayoutKind.Sequential, Size = 0x12)] internal struct Unknown036Storage { }
    [FieldOffset(0x048)] internal AttributeArray BaseAttributes;
    [FieldOffset(0x078)] internal float BonusExperience;
    [FieldOffset(0x07C)] internal AttributeArray EarnedAttributes;
    [FieldOffset(0x0AC)] internal uint Level;
    [FieldOffset(0x0B0)] internal AttributeArray EffectiveAttributes;
    [FieldOffset(0x0E0)] internal uint ScaledLevel;
    [FieldOffset(0x0E4)] internal uint Unknown0E4;
    [FieldOffset(0x0E8)] internal ChCliProgress.NotificationList Notifications;
    [FieldOffset(0x118)] internal ChCliCharacter* OwnerCharacter;
    [FieldOffset(0x120)] internal Unknown120Storage Unknown120;
    [StructLayout(LayoutKind.Sequential, Size = 0x0C)] internal struct Unknown120Storage { }
    [FieldOffset(0x12C)] internal Profession BaseProfession;
    [FieldOffset(0x130)] internal Unknown130Storage Unknown130;
    [StructLayout(LayoutKind.Sequential, Size = 0xD0)] internal struct Unknown130Storage { }
    [FieldOffset(0x200)] internal uint CachedLevel;
    [FieldOffset(0x204)] internal Unknown204Storage Unknown204;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)] internal struct Unknown204Storage { }
    [FieldOffset(0x234)] internal uint CachedScaledLevel;
    [FieldOffset(0x238)] internal uint Unknown238;
    [FieldOffset(0x23C)] internal float CachedBonusExperience;
    [FieldOffset(0x240)] internal Unknown240Storage Unknown240;
    [StructLayout(LayoutKind.Sequential, Size = 0x10)] internal struct Unknown240Storage { }
    [FieldOffset(0x250)] internal ChCliInventory* Inventory;
    [FieldOffset(0x258)] internal nint Unknown258;
    [FieldOffset(0x260)] internal Unknown260Storage Unknown260;
    [StructLayout(LayoutKind.Sequential, Size = 0x20)] internal struct Unknown260Storage { }
    [FieldOffset(0x280)] internal uint Unknown280;
    [FieldOffset(0x284)] internal Unknown284Storage Unknown284;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown284Storage { }
    [InlineArray(12)]
    internal struct AttributeArray
    {
        internal uint Element0;
    }
}
