using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0xA0)]
internal struct ItCliContext
{
    [FieldOffset(0x000)] internal nint ItCliContextVtable;
    [FieldOffset(0x008)] internal ushort Unknown008;
    [FieldOffset(0x00A)] internal ushort Unknown00A;
    [FieldOffset(0x010)] internal nint Unknown010;
    [FieldOffset(0x018)] internal uint Unknown018;
    [FieldOffset(0x020)] internal uint Unknown020;
    [FieldOffset(0x024)] internal uint Unknown024;
    [FieldOffset(0x028)] internal ushort Unknown028;
    [FieldOffset(0x02A)] internal ushort Unknown02A;
    // CItem* entries indexed by item id. The pointed-to item is the secondary
    // item interface returned by ItCliAgent construction, not the allocation
    // base of the owning ItCliAgent object.
    [FieldOffset(0x030)] internal nint ItemArray;
    [FieldOffset(0x038)] internal nint ItemArrayStorage;
    [FieldOffset(0x03C)] internal uint ItemCount;
    [FieldOffset(0x040)] internal uint Unknown040;
    [FieldOffset(0x048)] internal nint Unknown048;
    [FieldOffset(0x050)] internal Unknown050Storage Unknown050;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)] internal struct Unknown050Storage { }
    [FieldOffset(0x080)] internal uint DuplicateReportItemId;
    [FieldOffset(0x084)] internal uint DuplicateReportTimestamp;
    [FieldOffset(0x088)] internal uint ItemDefinitionMapBucketCount;
    [FieldOffset(0x08C)] internal uint ItemDefinitionMapCount;
    [FieldOffset(0x090)] internal nint ItemDefinitionMapEntries;
    [FieldOffset(0x098)] internal uint Unknown098;
    [FieldOffset(0x09C)] internal uint Unknown09C;
}
