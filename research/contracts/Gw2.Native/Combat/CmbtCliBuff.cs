using System.Runtime.InteropServices;

namespace Gw2.Native;

// Build-205.780 receive-side combat buff state. These structures describe decoded
// client state after a server message has been received; they are not an activation
// or send protocol.
internal static class CmbtCliBuffContract
{
    internal const int ActivateMessageHandlerRva = 0x12C19A0;
    internal const int ApplyMessageHandlerRva = 0x12C1A10;
    internal const int DeactivateMessageHandlerRva = 0x12C1B20;
    internal const int DurationUpdateMessageHandlerRva = 0x12C1B90;

    internal const int ActivateExistingRva = 0x12C0470;
    internal const int CreateOrUpdateRva = 0x12C0530;
    internal const int DeactivateExistingRva = 0x12C0A10;
    internal const int UpdateDurationRva = 0x12C0A90;
    internal const int ConstructorRva = 0x12BEE70;

    // Registration-table identifiers for decoded receive handlers. These ids
    // describe inbound client dispatch only; they are not novelty activation ids.
    internal const ushort ActivateReceiveMessageId = 0x02DE;
    internal const ushort ApplyReceiveMessageId = 0x02DF;
    internal const ushort DeactivateReceiveMessageId = 0x02E0;
    internal const ushort DurationUpdateReceiveMessageId = 0x02E1;

    // The character notification passes CmbtCliBuff + 0x10 to downstream
    // consumers, including AvCharEffect's event installer.
    internal const int EffectEventOffset = 0x10;
}

// Existing-Buff activation/reconciliation message consumed at RVA 0x12C19A0.
// RemainingDuration is used with the authored total Duration to reconstruct the
// client's ActiveStartTick when the Buff transitions to active.
[StructLayout(LayoutKind.Explicit, Size = 0x0E)]
internal struct CmbtCliBuffActivateMessage
{
    [FieldOffset(0x00)] internal ushort ReceiveHeader;
    [FieldOffset(0x02)] internal uint TargetCombatantId;
    [FieldOffset(0x06)] internal uint BuffId;
    [FieldOffset(0x0A)] internal uint RemainingDuration;
}

// Packed decoded apply record consumed at RVA 0x12C1A10. The handler's first
// parsed field begins at +0x02; +0x00 remains an opaque receive header.
[StructLayout(LayoutKind.Explicit, Size = 0x18)]
internal struct CmbtCliBuffApplyMessage
{
    [FieldOffset(0x00)] internal ushort ReceiveHeader;
    [FieldOffset(0x02)] internal uint TargetCombatantId;
    [FieldOffset(0x06)] internal uint SourceAgentId;
    [FieldOffset(0x0A)] internal uint SkillId;
    [FieldOffset(0x0E)] internal uint BuffId;
    [FieldOffset(0x12)] internal uint Duration;
    [FieldOffset(0x16)] internal byte Unknown16;
    [FieldOffset(0x17)] internal byte IsActive;
}

// Existing-Buff message consumed at RVA 0x12C1B20. The target Buff is found by
// BuffId, its Duration is replaced, IsActive is cleared, and the character is
// notified through a distinct callback.
[StructLayout(LayoutKind.Explicit, Size = 0x0E)]
internal struct CmbtCliBuffDeactivateMessage
{
    [FieldOffset(0x00)] internal ushort ReceiveHeader;
    [FieldOffset(0x02)] internal uint TargetCombatantId;
    [FieldOffset(0x06)] internal uint BuffId;
    [FieldOffset(0x0A)] internal uint Duration;
}

// Existing-Buff duration update consumed at RVA 0x12C1B90. Unlike the deactivate
// path, this changes Duration without forcibly clearing IsActive.
[StructLayout(LayoutKind.Explicit, Size = 0x0E)]
internal struct CmbtCliBuffDurationUpdateMessage
{
    [FieldOffset(0x00)] internal ushort ReceiveHeader;
    [FieldOffset(0x02)] internal uint TargetCombatantId;
    [FieldOffset(0x06)] internal uint BuffId;
    [FieldOffset(0x0A)] internal uint Duration;
}

// Resolved client Buff object constructed by CmbtCliBuff.cpp. Only fields proven
// by current static analysis are exposed; intrusive-list/ref bookkeeping remains
// intentionally opaque.
[StructLayout(LayoutKind.Explicit, Size = 0x78)]
internal struct CmbtCliBuff
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal uint SkillId;
    [FieldOffset(0x10)] internal nint SkillDefinition;
    [FieldOffset(0x18)] internal uint BuffId;
    [FieldOffset(0x20)] internal nint Owner;
    [FieldOffset(0x28)] internal nint SourceAgent;
    [FieldOffset(0x40)] internal uint Duration;
    [FieldOffset(0x44)] internal uint ActiveStartTick;
    [FieldOffset(0x48)] internal uint Unknown48;
    [FieldOffset(0x4C)] internal uint IsActive;
}

// View beginning at CmbtCliBuff + 0x10. The character Buff notification forwards
// this exact view to the world-effect event path rather than allocating a separate
// AvCharEffect event object.
[StructLayout(LayoutKind.Explicit, Size = 0x20)]
internal struct BuffEffectEvent
{
    [FieldOffset(0x00)] internal nint SkillDefinition;
    [FieldOffset(0x08)] internal uint BuffId;
    [FieldOffset(0x10)] internal nint Owner;
    [FieldOffset(0x18)] internal nint SourceAgent;
}
