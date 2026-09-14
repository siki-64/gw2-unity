using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0xB08)]
internal unsafe struct ChCliCharacter
{
    [FieldOffset(0x000)] internal Unknown000Storage Unknown000;
    [StructLayout(LayoutKind.Explicit, Size = 0x50)]
    internal struct Unknown000Storage
    {
        [FieldOffset(0x000)] internal nint Vtable;
        [FieldOffset(0x008)] internal nint InterfaceVtable;
        [FieldOffset(0x010)] internal nint Unknown010Vtable;
        [FieldOffset(0x018)] internal nint Unknown018Vtable;
        [FieldOffset(0x020)] internal nint Unknown020Vtable;
        [FieldOffset(0x028)] internal nint Unknown028Vtable;
        [FieldOffset(0x030)] internal nint Unknown030Vtable;
        [FieldOffset(0x038)] internal nint Unknown038Vtable;
        [FieldOffset(0x040)] internal nint Unknown040Vtable;
        [FieldOffset(0x048)] internal nint Unknown048Vtable;
    }
    [FieldOffset(0x050)] internal Unknown050Storage Unknown050;
    [StructLayout(LayoutKind.Explicit, Size = 0x48)]
    internal struct Unknown050Storage
    {
        [FieldOffset(0x00)] internal nint Vtable;
        [FieldOffset(0x08)] internal nint Unknown058;
        [FieldOffset(0x10)] internal uint Unknown060;
        [FieldOffset(0x14)] internal uint Unknown064;
        [FieldOffset(0x18)] internal uint Unknown068;
        [FieldOffset(0x1C)] internal int Unknown06C;
        [FieldOffset(0x20)] internal TaggedSentinelStorage Unknown070;
        [FieldOffset(0x40)] internal nint Unknown090;
    }
    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    internal struct TaggedSentinelStorage
    {
        [FieldOffset(0x00)] internal uint Marker;
        [FieldOffset(0x04)] internal uint Unknown004;
        [FieldOffset(0x08)] internal nint Sentinel;
        [FieldOffset(0x10)] internal nint TaggedSentinel;
        [FieldOffset(0x18)] internal ushort AllocatorId;
        [FieldOffset(0x1A)] internal ushort AllocatorGeneration;
        [FieldOffset(0x1C)] internal uint Unknown01C;
    }
    [FieldOffset(0x098)] internal Agent* Unknown98;
    [FieldOffset(0x0A0)] internal uint UnknownA0;
    [FieldOffset(0x0A4)] internal Unknown0A4Storage Unknown0A4;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown0A4Storage { }
    [FieldOffset(0x0A8)] internal nint SpeciesDef;
    [FieldOffset(0x0B0)] internal Unknown0B0Storage Unknown0B0;
    [StructLayout(LayoutKind.Sequential, Size = 0x10)] internal struct Unknown0B0Storage { }
    [FieldOffset(0x0C0)] internal Attitude AttitudeTowardControlled;
    [FieldOffset(0x0C4)] internal Unknown0C4Storage Unknown0C4;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown0C4Storage { }
    [FieldOffset(0x0C8)] internal CmbtCliBreakBar* Breakbar;
    [FieldOffset(0x0D0)] internal Unknown0D0Storage Unknown0D0;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Unknown0D0Storage { }
    [FieldOffset(0x0D8)] internal CharacterWaterState WaterState;
    [FieldOffset(0x0DC)] internal Unknown0DCStorage Unknown0DC;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown0DCStorage { }
    [FieldOffset(0x0E0)] internal CombatantNotifyListStorage CombatantNotifyList;
    [StructLayout(LayoutKind.Explicit, Size = 0x30)]
    internal struct CombatantNotifyListStorage
    {
        [FieldOffset(0x00)] internal uint Unknown000;
        [FieldOffset(0x08)] internal nint Unknown008;
        [FieldOffset(0x10)] internal nint Unknown010;
        [FieldOffset(0x18)] internal nint Unknown018;
        [FieldOffset(0x20)] internal nint Unknown020;
        [FieldOffset(0x28)] internal uint Unknown028;
    }
    [FieldOffset(0x110)] internal nint CompositeData;
    [FieldOffset(0x118)] internal AllocatorReferenceStorage Unknown118;
    [FieldOffset(0x128)] internal AllocatorReferenceStorage Unknown128;
    [FieldOffset(0x138)] internal AllocatorReferenceStorage Unknown138;
    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    internal struct AllocatorReferenceStorage
    {
        [FieldOffset(0x00)] internal ushort AllocatorId;
        [FieldOffset(0x02)] internal ushort AllocatorGeneration;
        [FieldOffset(0x04)] internal uint Unknown004;
        [FieldOffset(0x08)] internal nint Value;
    }
    [FieldOffset(0x148)] internal Unknown148Storage Unknown148;
    [StructLayout(LayoutKind.Explicit, Size = 0x30)]
    internal struct Unknown148Storage
    {
        [FieldOffset(0x00)] internal ushort AllocatorId;
        [FieldOffset(0x02)] internal ushort AllocatorGeneration;
        [FieldOffset(0x04)] internal uint Unknown004;
        [FieldOffset(0x08)] internal nint Unknown008;
        [FieldOffset(0x10)] internal nint Unknown010;
        [FieldOffset(0x18)] internal uint Unknown018;
        [FieldOffset(0x1C)] internal uint Unknown01C;
        [FieldOffset(0x20)] internal uint Unknown020;
        [FieldOffset(0x24)] internal byte Unknown024;
        [FieldOffset(0x25)] internal Unknown025Storage Unknown025;
        [FieldOffset(0x28)] internal nint Unknown028;
        [StructLayout(LayoutKind.Sequential, Size = 0x03)]
        internal struct Unknown025Storage { }
    }
    [FieldOffset(0x178)] internal ChCliCharacterFlag Flags;
    [FieldOffset(0x180)] internal Unknown180Storage Unknown180;
    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    internal struct Unknown180Storage
    {
        [FieldOffset(0x00)] internal float Unknown000;
        [FieldOffset(0x04)] internal uint Unknown004;
        [FieldOffset(0x08)] internal nint Unknown008;
        [FieldOffset(0x10)] internal nint Unknown010;
    }
    [FieldOffset(0x198)] internal Unknown198Storage Unknown198;
    [StructLayout(LayoutKind.Explicit, Size = 0x30)]
    internal struct Unknown198Storage
    {
        [FieldOffset(0x00)] internal ushort AllocatorId;
        [FieldOffset(0x02)] internal ushort AllocatorGeneration;
        [FieldOffset(0x04)] internal uint Unknown004;
        [FieldOffset(0x08)] internal nint Unknown008;
        [FieldOffset(0x10)] internal nint Unknown010;
        [FieldOffset(0x18)] internal nint Unknown018;
        [FieldOffset(0x20)] internal nint Unknown020;
        [FieldOffset(0x28)] internal uint Unknown028;
    }
    [FieldOffset(0x1C8)] internal Unknown1C8Storage Unknown1C8;
    [StructLayout(LayoutKind.Sequential, Size = 0x18)]
    internal struct Unknown1C8Storage { }
    [FieldOffset(0x1E0)] internal ChCliProgress.NotificationList Unknown1E0;
    [FieldOffset(0x210)] internal uint Unknown210;
    [FieldOffset(0x214)] internal Unknown214Storage Unknown214;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown214Storage { }
    [FieldOffset(0x218)] internal nint Unknown218;
    [FieldOffset(0x220)] internal uint Unknown220;
    [FieldOffset(0x224)] internal Unknown224Storage Unknown224;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown224Storage { }
    [FieldOffset(0x228)] internal nint RenownSubRegion;
    [FieldOffset(0x230)] internal Unknown230Storage Unknown230;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown230Storage { }
    [FieldOffset(0x234)] internal SkillChallengeBitGuidStorage SkillChallengeBitGuid;
    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    internal struct SkillChallengeBitGuidStorage
    {
        [FieldOffset(0x00)] internal uint Word0;
        [FieldOffset(0x04)] internal uint Word1;
        [FieldOffset(0x08)] internal uint Word2;
        [FieldOffset(0x0C)] internal uint Word3;
    }
    [FieldOffset(0x244)] internal Unknown244Storage Unknown244;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown244Storage { }
    [FieldOffset(0x248)] internal ulong Unknown248;
    [FieldOffset(0x250)] internal float Unknown250;
    [FieldOffset(0x254)] internal Unknown254Storage Unknown254;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown254Storage { }
    [FieldOffset(0x258)] internal nint Unknown258;
    [FieldOffset(0x260)] internal SpeciesDerivedDataStorage SpeciesDerivedData;
    [StructLayout(LayoutKind.Explicit, Size = 0x28)]
    internal struct SpeciesDerivedDataStorage
    {
        [FieldOffset(0x00)] internal uint Unknown000;
        [FieldOffset(0x04)] internal uint Unknown004;
        [FieldOffset(0x08)] internal uint Unknown008;
        [FieldOffset(0x0C)] internal uint Unknown00C;
        [FieldOffset(0x10)] internal float Unknown010;
        [FieldOffset(0x14)] internal uint Unknown014;
        [FieldOffset(0x18)] internal uint Unknown018;
        [FieldOffset(0x1C)] internal uint Unknown01C;
        [FieldOffset(0x20)] internal nint Unknown020;
    }
    [FieldOffset(0x288)] internal uint Unknown288;
    [FieldOffset(0x28C)] internal uint Unknown28C;
    [FieldOffset(0x290)] internal uint Unknown290;
    [FieldOffset(0x294)] internal Unknown294Storage Unknown294;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown294Storage { }
    [FieldOffset(0x298)] internal nint Unknown298;
    [FieldOffset(0x2A0)] internal uint Unknown2A0;
    [FieldOffset(0x2A4)] internal Unknown2A4Storage Unknown2A4;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown2A4Storage { }
    [FieldOffset(0x2A8)] internal Unknown2A8FloatCollection Unknown2A8;
    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    internal struct Unknown2A8FloatCollection
    {
        [FieldOffset(0x00)] internal ushort AllocatorId;
        [FieldOffset(0x02)] internal ushort AllocatorGeneration;
        [FieldOffset(0x08)] internal float* Values;
        [FieldOffset(0x10)] internal uint Capacity;
        [FieldOffset(0x14)] internal uint Count;
        [FieldOffset(0x18)] internal uint Growth;
        [FieldOffset(0x1C)] internal uint Unknown1C;
    }
    [FieldOffset(0x2C8)] internal float Unknown2C8;
    [FieldOffset(0x2CC)] internal float Unknown2CC;
    [FieldOffset(0x2D0)] internal float Unknown2D0;
    [FieldOffset(0x2D4)] internal Unknown2D4Storage Unknown2D4;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)] internal struct Unknown2D4Storage { }
    [FieldOffset(0x2D8)] internal Unknown2D8EntryCollection Unknown2D8;
    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    internal struct Unknown2D8EntryCollection
    {
        [FieldOffset(0x00)] internal ushort AllocatorId;
        [FieldOffset(0x02)] internal ushort AllocatorGeneration;
        [FieldOffset(0x08)] internal Unknown2D8Entry* Entries;
        [FieldOffset(0x10)] internal uint Capacity;
        [FieldOffset(0x14)] internal uint Count;
        [FieldOffset(0x18)] internal uint Growth;
        [FieldOffset(0x1C)] internal uint Unknown1C;
    }
    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    internal struct Unknown2D8Entry
    {
        [FieldOffset(0x00)] internal nint Unknown00;
        [FieldOffset(0x08)] internal uint Unknown08;
        [FieldOffset(0x0C)] internal uint Unknown0C;
        [FieldOffset(0x10)] internal uint Unknown10;
        [FieldOffset(0x14)] internal float Unknown14;
    }
    [FieldOffset(0x2F8)] internal Unknown2F8Storage Unknown2F8;
    [StructLayout(LayoutKind.Explicit, Size = 0x30)]
    internal struct Unknown2F8Storage
    {
        [FieldOffset(0x00)] internal Unknown2F8InputStorage Input;
        [FieldOffset(0x20)] internal Unknown2F8SnapshotStorage Snapshot;
    }
    [StructLayout(LayoutKind.Sequential, Size = 0x1C)]
    internal struct Unknown2F8InputStorage { }
    [StructLayout(LayoutKind.Sequential, Size = 0x0C)]
    internal struct Unknown2F8SnapshotStorage { }
    [FieldOffset(0x328)] internal Unknown328Storage Unknown328;
    [StructLayout(LayoutKind.Sequential, Size = 0x08)] internal struct Unknown328Storage { }
    [FieldOffset(0x330)] internal ChCliChatter Chatter;
    [FieldOffset(0x388)] internal ChCliCoreStats* CoreStats;
    [FieldOffset(0x390)] internal Unknown390Storage Unknown390;
    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    internal struct Unknown390Storage
    {
        [FieldOffset(0x000)] internal nint Vtable;
        [FieldOffset(0x008)] internal uint Unknown008;
        [FieldOffset(0x00C)] internal Unknown00CStorage Unknown00C;
        [FieldOffset(0x010)] internal ChCliProgress.NotificationList Notifications;
        [StructLayout(LayoutKind.Sequential, Size = 0x04)]
        internal struct Unknown00CStorage { }
    }
    [FieldOffset(0x3D0)] internal ChCliEndurance* Endurance;
    [FieldOffset(0x3D8)] internal ChCliEnergies* Energies;
    [FieldOffset(0x3E0)] internal ChCliAdventure* Adventure;
    [FieldOffset(0x3E8)] internal ChCliHealth* Health;
    [FieldOffset(0x3F0)] internal ChCliInventory* Inventory;
    [FieldOffset(0x3F8)] internal ChCliKennel* Kennel;
    [FieldOffset(0x400)] internal ChCliMovement Movement;
    [FieldOffset(0x4B0)] internal ChCliOrder Order;
    [FieldOffset(0x510)] internal ChCliProfession* Profession;
    [FieldOffset(0x518)] internal PvpGearProvider* Unknown518;
    [FieldOffset(0x520)] internal ChCliSkillbar* Skillbar;
    [FieldOffset(0x528)] internal TaggedSentinelStorage Unknown528;
    [FieldOffset(0x548)] internal ChCliTransformation Transformation;
    [FieldOffset(0xB00)] internal ChCliWardrobe* Wardrobe;
}
