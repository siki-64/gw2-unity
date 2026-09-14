using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0x68)]
internal unsafe struct SkillDefinition
{
    // CnContext virtual +0x70 resolves SkillDefinition by this content class and SkillId.
    internal const uint ContentClass = 0x40;

    [FieldOffset(0x000)] internal Unknown000Storage Unknown000;
    [StructLayout(LayoutKind.Sequential, Size = 0x28)]
    internal struct Unknown000Storage { }

    // AsBuff.cpp and the native content resolver prove +0x28 is the numeric skill id.
    // AvCharEffect uses the same SkillId as the key for its per-character runtime table.
    [FieldOffset(0x028)] internal uint SkillId;

    [FieldOffset(0x038)] internal SkillDefinitionFlag Flags;

    // Authored world-effect applications. Each record is 0x20 bytes and supplies
    // its IEffectDef independently of AvCharEffect's created runtime entries.
    [FieldOffset(0x040)] internal SkillEffectApplicationEntry* EffectApplications;
    [FieldOffset(0x048)] internal uint EffectApplicationCount;

    // Native tagged subtype payload. Assertion sites for GetAbility()/GetBuff()
    // prove the tag values below.
    [FieldOffset(0x058)] internal PayloadKind PayloadType;
    [FieldOffset(0x060)] internal nint Payload;

    internal enum PayloadKind : uint
    {
        Ability = 0,
        Buff = 1,
    }
}
