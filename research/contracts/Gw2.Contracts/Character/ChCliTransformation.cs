using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x5B8)]
internal struct ChCliTransformation
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal ChCliProgress.NotificationList Notifications;
    [FieldOffset(0x038)] internal float Unknown038;
    [FieldOffset(0x03C)] internal float Unknown03C;
    [FieldOffset(0x040)] internal TransformationRecordArray Records;
    [InlineArray(6)]
    internal struct TransformationRecordArray
    {
        internal TransformationRecord Element0;
    }
    [StructLayout(LayoutKind.Explicit, Size = 0xE8)]
    internal struct TransformationRecord
    {
        [FieldOffset(0x000)] internal Unknown128Storage Unknown000;
        [FieldOffset(0x010)] internal uint Unknown010;
        [FieldOffset(0x014)] internal uint Unknown014;
        [FieldOffset(0x018)] internal Unknown128Storage Unknown018;
        [FieldOffset(0x028)] internal nint Unknown028;
        [FieldOffset(0x030)] internal nint Unknown030;
        [FieldOffset(0x038)] internal nint Unknown038;
        [FieldOffset(0x040)] internal nint Unknown040;
        [FieldOffset(0x048)] internal uint Unknown048;
        [FieldOffset(0x04C)] internal Unknown04CStorage Unknown04C;
        [StructLayout(LayoutKind.Sequential, Size = 0x04)]
        internal struct Unknown04CStorage { }
        [FieldOffset(0x050)] internal nint Unknown050;
        [FieldOffset(0x058)] internal nint Unknown058;
        [FieldOffset(0x060)] internal uint Unknown060;
        [FieldOffset(0x064)] internal Unknown064Storage Unknown064;
        [StructLayout(LayoutKind.Sequential, Size = 0x04)]
        internal struct Unknown064Storage { }
        [FieldOffset(0x068)] internal nint Unknown068;
        [FieldOffset(0x070)] internal uint Unknown070;
        [FieldOffset(0x074)] internal Unknown074Storage Unknown074;
        [StructLayout(LayoutKind.Sequential, Size = 0x04)]
        internal struct Unknown074Storage { }
        [FieldOffset(0x078)] internal float Unknown078;
        [FieldOffset(0x07C)] internal float Unknown07C;
        [FieldOffset(0x080)] internal nint Unknown080;
        [FieldOffset(0x088)] internal nint Unknown088;
        [FieldOffset(0x090)] internal nint Unknown090;
        [FieldOffset(0x098)] internal nint Unknown098;
        [FieldOffset(0x0A0)] internal nint Unknown0A0;
        [FieldOffset(0x0A8)] internal Unknown0A8Storage Unknown0A8;
        [StructLayout(LayoutKind.Sequential, Size = 0x08)]
        internal struct Unknown0A8Storage { }
        [FieldOffset(0x0B0)] internal uint Unknown0B0;
        [FieldOffset(0x0B4)] internal uint Unknown0B4;
        [FieldOffset(0x0B8)] internal uint RepresentationKind;
        [FieldOffset(0x0BC)] internal Unknown0BCStorage Unknown0BC;
        [StructLayout(LayoutKind.Sequential, Size = 0x04)]
        internal struct Unknown0BCStorage { }
        [FieldOffset(0x0C0)] internal nint AlternateRepresentation;
        [FieldOffset(0x0C8)] internal DynamicStorage Dynamic;
        [StructLayout(LayoutKind.Explicit, Size = 0x20)]
        internal struct DynamicStorage
        {
            [FieldOffset(0x00)] internal ushort AllocatorId;
            [FieldOffset(0x02)] internal ushort AllocatorGeneration;
            [FieldOffset(0x04)] internal uint Unknown04;
            [FieldOffset(0x08)] internal nint Data;
            [FieldOffset(0x10)] internal ulong Unknown10;
            [FieldOffset(0x18)] internal uint Growth;
            [FieldOffset(0x1C)] internal uint Unknown1C;
        }
        [StructLayout(LayoutKind.Explicit, Size = 0x10)]
        internal struct Unknown128Storage
        {
            [FieldOffset(0x00)] internal ulong Low;
            [FieldOffset(0x08)] internal ulong High;
        }
    }
    [FieldOffset(0x5B0)] internal uint Unknown5B0;
    [FieldOffset(0x5B4)] internal Unknown5B4Storage Unknown5B4;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)]
    internal struct Unknown5B4Storage { }
}
