using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0xC0)]
internal unsafe struct ChCliSpecialization
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal nint ProgressListenerVtable;
    [FieldOffset(0x010)] internal ChCliProgress.NotificationList Notifications;
    [FieldOffset(0x040)] internal SpecializationDefinition* RequestedSlot0;
    [FieldOffset(0x048)] internal SpecializationDefinition* RequestedSlot1;
    [FieldOffset(0x050)] internal SpecializationDefinition* RequestedSlot2;
    [FieldOffset(0x058)] internal SpecializationDefinition* SelectedSlot0;
    [FieldOffset(0x060)] internal SpecializationDefinition* SelectedSlot1;
    [FieldOffset(0x068)] internal SpecializationDefinition* SelectedSlot2;
    [FieldOffset(0x070)] internal byte ProgressTierCountSlot0;
    [FieldOffset(0x071)] internal byte ProgressTierCountSlot1;
    [FieldOffset(0x072)] internal byte ProgressTierCountSlot2;
    [FieldOffset(0x073)] internal Unknown073Storage Unknown073;
    [StructLayout(LayoutKind.Sequential, Size = 0x05)] internal struct Unknown073Storage { }
    [FieldOffset(0x078)] internal TraitDefinition* SelectedMajorTrait00;
    [FieldOffset(0x080)] internal TraitDefinition* SelectedMajorTrait01;
    [FieldOffset(0x088)] internal TraitDefinition* SelectedMajorTrait02;
    [FieldOffset(0x090)] internal TraitDefinition* SelectedMajorTrait10;
    [FieldOffset(0x098)] internal TraitDefinition* SelectedMajorTrait11;
    [FieldOffset(0x0A0)] internal TraitDefinition* SelectedMajorTrait12;
    [FieldOffset(0x0A8)] internal TraitDefinition* SelectedMajorTrait20;
    [FieldOffset(0x0B0)] internal TraitDefinition* SelectedMajorTrait21;
    [FieldOffset(0x0B8)] internal TraitDefinition* SelectedMajorTrait22;
}
