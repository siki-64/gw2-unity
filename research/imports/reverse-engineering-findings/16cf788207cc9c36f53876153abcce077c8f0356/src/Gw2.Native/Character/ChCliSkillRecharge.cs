using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0x70)]
internal struct ChCliSkillRecharge
{
    // Tick-like dword sampled alongside active recharge entries.
    [FieldOffset(0x024)] internal uint Unknown024;
    // Float sampled alongside Unknown024. Its exact time/rate role is unresolved.
    [FieldOffset(0x030)] internal float Unknown030;
    [FieldOffset(0x048)] internal nint SkillRechargeList;
    [FieldOffset(0x068)] internal nint SlotRechargeList;
}

[StructLayout(LayoutKind.Explicit, Size = 0x30)]
internal struct ChCliSkillRechargeEntry
{
    [FieldOffset(0x010)] internal uint Unknown010;
    [FieldOffset(0x020)] internal uint Unknown020;
    [FieldOffset(0x024)] internal uint Unknown024;
    [FieldOffset(0x02C)] internal uint Unknown02C;
}
