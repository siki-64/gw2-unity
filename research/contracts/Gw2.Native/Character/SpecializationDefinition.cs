using System.Runtime.InteropServices;

namespace Gw2.Native;

[Flags]
internal enum SpecializationDefinitionFlags : uint
{
    None = 0,
    // Native specialization validation rejects this flag in slots 0 and 1 and accepts it only in slot 2.
    Elite = 1 << 0
}

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct SpecializationDefinition
{
    [FieldOffset(0x000)] internal Unknown000Storage Unknown000;
    [StructLayout(LayoutKind.Sequential, Size = 0x10)] internal struct Unknown000Storage { }
    [FieldOffset(0x010)] internal uint DefinitionType;
    [FieldOffset(0x014)] internal SpecializationContentId Id;
    [FieldOffset(0x018)] internal Unknown018Storage Unknown018;
    [StructLayout(LayoutKind.Sequential, Size = 0x20)] internal struct Unknown018Storage { }
    [FieldOffset(0x038)] internal SpecializationDefinitionFlags Flags;
    [FieldOffset(0x03C)] internal uint Unknown3C;
    [FieldOffset(0x040)] internal Unknown040Storage Unknown040;
    [StructLayout(LayoutKind.Sequential, Size = 0x10)] internal struct Unknown040Storage { }
    [FieldOffset(0x050)] internal SpecializationProgressionDefinition* ProgressionDefinition;
    [FieldOffset(0x058)] internal TraitDefinition** TraitDefinitions;
    [FieldOffset(0x060)] internal uint TraitDefinitionCount;
}
