using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit, Size = 0xC0)]
internal unsafe struct BtHero
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x08)] internal Interface008Storage Interface008;
    [StructLayout(LayoutKind.Sequential, Size = 0x50)] internal struct Interface008Storage { }
    [FieldOffset(0x58)] internal nint Interface58;
    [FieldOffset(0x60)] internal nint Interface60;
    [FieldOffset(0x68)] internal nint PvpHeroCollectionListenerVtable;
    [FieldOffset(0x70)] internal ChCliPvpHeroCollection* PvpHeroCollection;
    [FieldOffset(0x78)] internal ushort Unknown78;
    [FieldOffset(0x7A)] internal ushort Unknown7A;
    [FieldOffset(0x7C)] internal uint Unknown7C;
    [FieldOffset(0x80)] internal nint Unknown80;
    [FieldOffset(0x88)] internal nint Unknown88;
    [FieldOffset(0x90)] internal int Unknown90;
    [FieldOffset(0x94)] internal uint Unknown94;
    [FieldOffset(0x98)] internal ushort Unknown98;
    [FieldOffset(0x9A)] internal ushort Unknown9A;
    [FieldOffset(0x9C)] internal uint Unknown9C;
    [FieldOffset(0xA0)] internal nint EntryPointers;
    [FieldOffset(0xA8)] internal uint UnknownA8;
    [FieldOffset(0xAC)] internal uint EntryCount;
    [FieldOffset(0xB0)] internal int UnknownB0;
    [FieldOffset(0xB4)] internal uint UnknownB4;
    [FieldOffset(0xB8)] internal uint UnknownB8;
    [FieldOffset(0xBC)] internal uint UnknownBC;
}
