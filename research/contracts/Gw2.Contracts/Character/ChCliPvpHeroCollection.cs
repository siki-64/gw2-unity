using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x60)]
internal struct ChCliPvpHeroCollection
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal ushort Unknown08;
    [FieldOffset(0x0A)] internal ushort Unknown0A;
    [FieldOffset(0x0C)] internal uint Unknown0C;

    // A vtable method indexes this dword table with hero-definition Id >> 5 and tests Id & 31.
    [FieldOffset(0x10)] internal nint UnlockedHeroBitWords;
    [FieldOffset(0x18)] internal uint Unknown18;
    [FieldOffset(0x1C)] internal uint UnlockedHeroWordCount;

    [FieldOffset(0x20)] internal uint Unknown20;
    [FieldOffset(0x24)] internal uint Unknown24;
    [FieldOffset(0x28)] internal nint Unknown28;
    [FieldOffset(0x30)] internal ListenerStorage Unknown30;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)] internal struct ListenerStorage { }
}
