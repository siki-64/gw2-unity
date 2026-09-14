using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0xE0)]
internal unsafe struct ChCliSkill
{
    internal const int VtableSlot6FieldOffset = 0x58;
    internal const int VtableSlot8FieldOffset = 0x5C;
    internal const int ConfiguredSkillsAOffset = 0x60;
    internal const int ConfiguredSkillsBOffset = 0x88;
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal Unknown008TableStorage Unknown008;
    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    internal struct Unknown008TableStorage
    {
        [FieldOffset(0x00)] internal uint BucketCount;
        [FieldOffset(0x04)] internal uint Count;
        [FieldOffset(0x08)] internal UnknownTableEntry* Entries;
        [FieldOffset(0x10)] internal uint Unknown10;
        [FieldOffset(0x14)] internal uint Unknown14;
        [FieldOffset(0x18)] internal ushort Unknown18;
        [FieldOffset(0x1A)] internal ushort Unknown1A;
        [FieldOffset(0x1C)] internal uint Unknown1C;
    }
    [FieldOffset(0x028)] internal uint* Unknown028BitWords;
    [FieldOffset(0x030)] internal uint Unknown030;
    [FieldOffset(0x034)] internal uint Unknown034BitWordCount;
    [FieldOffset(0x038)] internal uint Unknown038;
    [FieldOffset(0x03C)] internal Unknown03CStorage Unknown03C;
    [StructLayout(LayoutKind.Sequential, Size = 0x04)]
    internal struct Unknown03CStorage { }
    [FieldOffset(0x040)] internal Unknown040TableStorage Unknown040;
    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    internal struct Unknown040TableStorage
    {
        [FieldOffset(0x00)] internal uint BucketCount;
        [FieldOffset(0x04)] internal uint Count;
        [FieldOffset(0x08)] internal UnknownTableEntry* Entries;
        [FieldOffset(0x10)] internal uint Unknown10;
        [FieldOffset(0x14)] internal uint Unknown14;
    }
    [FieldOffset(0x058)] internal uint Unknown058;
    [FieldOffset(0x05C)] internal uint Unknown05C;
    [FieldOffset(0x060)] internal SkillDefinitionPointerArray ConfiguredSkillsA;
    [FieldOffset(0x088)] internal SkillDefinitionPointerArray ConfiguredSkillsB;
    [InlineArray(5)]
    internal struct SkillDefinitionPointerArray
    {
        internal nint Element0;
    }
    [FieldOffset(0x0B0)] internal NotifierStorage Notifier;
    [StructLayout(LayoutKind.Explicit, Size = 0x30)]
    internal struct NotifierStorage
    {
        [FieldOffset(0x00)] internal uint Unknown00;
        [FieldOffset(0x08)] internal nint Unknown08;
        [FieldOffset(0x10)] internal nint Unknown10;
        [FieldOffset(0x18)] internal nint Unknown18;
        [FieldOffset(0x20)] internal nint Unknown20;
        [FieldOffset(0x28)] internal uint Unknown28;
    }
    [StructLayout(LayoutKind.Explicit, Size = 0x0C)]
    internal struct UnknownTableEntry
    {
        [FieldOffset(0x00)] internal uint Unknown00;
        [FieldOffset(0x04)] internal uint Unknown04;
        [FieldOffset(0x08)] internal uint Unknown08;
    }
}
[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x0C)]
internal struct ChCliConfiguredSkillUpdate
{
    [FieldOffset(0x00)] internal ChCliSkillMessageId MessageId;
    [FieldOffset(0x02)] internal SkillContentId SkillContentId;
    [FieldOffset(0x06)] internal ChCliConfiguredSkillSlot SkillSlot;
    [FieldOffset(0x07)] internal ChCliSkillContextCode SkillContext;
    [FieldOffset(0x08)] internal uint PlayerListIndex;
}
