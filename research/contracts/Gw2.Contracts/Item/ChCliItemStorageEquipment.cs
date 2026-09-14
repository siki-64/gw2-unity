using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x730)]
internal struct ChCliItemStorageEquipment
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal EquipmentRecordArray EquipmentRecords;
    [InlineArray(20)]
    internal struct EquipmentRecordArray
    {
        internal EquipmentRecord Element0;
    }
    [FieldOffset(0x6E8)] internal nint PvpRune;
    [FieldOffset(0x6F0)] internal nint PvpRelic;
    [FieldOffset(0x6F8)] internal nint PvpAmulet;
    [FieldOffset(0x700)] internal PvpSigilArray PvpSigils;
    [InlineArray(4)]
    internal struct PvpSigilArray
    {
        internal nint Element0;
    }
    [FieldOffset(0x720)] internal ushort NameAllocatorId;
    [FieldOffset(0x722)] internal ushort NameAllocatorGeneration;
    [FieldOffset(0x724)] internal uint Unknown724;
    [FieldOffset(0x728)] internal nint NameBuffer;
    [StructLayout(LayoutKind.Explicit, Size = 0x58)]
    internal struct EquipmentRecord
    {
        [FieldOffset(0x00)] internal uint Unknown00;
        [FieldOffset(0x04)] internal uint Unknown04;
        [FieldOffset(0x08)] internal uint ItemId;
        [FieldOffset(0x0C)] internal uint Unknown0C;
        [FieldOffset(0x10)] internal nint UpgradeDefinition;
        [FieldOffset(0x18)] internal SuffixDefinitionArray SuffixDefinitions;
        [InlineArray(2)] internal struct SuffixDefinitionArray { internal nint Element0; }
        [FieldOffset(0x28)] internal InfusionDefinitionArray InfusionDefinitions;
        [InlineArray(3)] internal struct InfusionDefinitionArray { internal nint Element0; }
        [FieldOffset(0x38)] internal nint Unknown38;
        [FieldOffset(0x40)] internal uint ItemDefinitionId;
        [FieldOffset(0x44)] internal uint UpgradeDefinitionId;
        [FieldOffset(0x48)] internal SuffixDefinitionIdArray SuffixDefinitionIds;
        [InlineArray(2)] internal struct SuffixDefinitionIdArray { internal uint Element0; }
        [FieldOffset(0x50)] internal uint InfusionDefinitionId0;
        [FieldOffset(0x54)] internal uint InfusionDefinitionId1;
    }
}
