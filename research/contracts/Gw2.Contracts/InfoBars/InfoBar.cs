using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x130)]
internal unsafe struct InfoBar
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal nint Interface08;
    [FieldOffset(0x10)] internal int WidgetId;
    [FieldOffset(0x14)] internal int Unknown14;
    [FieldOffset(0x18)] internal nint Unknown18;
    [FieldOffset(0x20)] internal long Unknown20;
    [FieldOffset(0x28)] internal long Unknown28;
    [FieldOffset(0x30)] internal long Unknown30;
    [FieldOffset(0x38)] internal long Unknown38;
    [FieldOffset(0x40)] internal long Unknown40;
    [FieldOffset(0x48)] internal long Unknown48;
    [FieldOffset(0x50)] internal nint Interface50;
    [FieldOffset(0x58)] internal nint CharacterTrackedClient;
    [FieldOffset(0x60)] internal nint Interface60;
    [FieldOffset(0x68)] internal nint Interface68;
    [FieldOffset(0x70)] internal nint Interface70;
    [FieldOffset(0x78)] internal nint Interface78;
    [FieldOffset(0x80)] internal nint Interface80;
    [FieldOffset(0x88)] internal nint Interface88;
    [FieldOffset(0x90)] internal nint Interface90;
    [FieldOffset(0x98)] internal nint Interface98;
    [FieldOffset(0xA0)] internal nint Unit;
    [FieldOffset(0xA8)] internal nint UnitNext;
    [FieldOffset(0xB0)] internal nint UnitPrevious;
    [FieldOffset(0xB8)] internal ChCliCharacter* Character;
    [FieldOffset(0xC0)] internal nint PlayerLookupResult;
    // Channels are named by storage offset: their consumer sets overlap.
    // See research/contracts/notes/InfoBars/native-layout.md for instruction evidence.
    [FieldOffset(0xC8)] internal InfoBarPresentationSmoother PresentationC8;
    [FieldOffset(0xDC)] internal InfoBarPresentationSmoother PresentationDC;
    [FieldOffset(0xF0)] internal InfoBarPresentationSmoother PresentationF0;
    [FieldOffset(0x104)] internal float AnimationPhase;
    [FieldOffset(0x108)] internal float AccumulatedUpdateTime;
    [FieldOffset(0x10C)] internal byte Flags10C;
    [FieldOffset(0x10D)] internal byte Unknown10D;
    [FieldOffset(0x10E)] internal byte Unknown10E;
    [FieldOffset(0x10F)] internal byte Unknown10F;
    [FieldOffset(0x110)] internal uint LastSubwidgetMessageType;
    [FieldOffset(0x114)] internal float Unknown114;
    [FieldOffset(0x118)] internal SubwidgetMessageReference LastSubwidgetMessage;
    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    internal struct SubwidgetMessageReference
    {
        [FieldOffset(0x00)] internal nint Value;
        [FieldOffset(0x08)] internal nint Next;
        [FieldOffset(0x10)] internal nint Previous;
    }
}
