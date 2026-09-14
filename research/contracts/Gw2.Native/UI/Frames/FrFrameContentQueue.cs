using System.Runtime.InteropServices;

namespace Gw2.Native;

// Constructor 0x141074720 initializes through +0x67. Frame state is not queue storage.
[StructLayout(LayoutKind.Explicit, Size = 0x68)]
internal struct FrFrameContentQueue
{
    [FieldOffset(0x000)] internal NativeArrayHeader OrderedEntries;
    [FieldOffset(0x030)] internal NativeArrayHeader Layers;
    [FieldOffset(0x050)] internal float Opacity;
    [FieldOffset(0x058)] internal nint PendingLink;
    [FieldOffset(0x060)] internal nuint PendingLinkTagged;
    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    internal struct NativeArrayHeader
    {
        [FieldOffset(0x08)] internal nint Data;
        [FieldOffset(0x10)] internal uint Capacity;
        [FieldOffset(0x14)] internal uint Count;
        [FieldOffset(0x18)] internal uint Growth;
    }
    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    internal struct FrFrameContentLayer
    {
        [FieldOffset(0x08)] internal nint Entries;
        [FieldOffset(0x10)] internal uint Capacity;
        [FieldOffset(0x14)] internal uint Count;
        [FieldOffset(0x18)] internal uint Growth;
    }
    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    internal struct FrFrameContentEntry
    {
        [FieldOffset(0x00)] internal nint Model;
        [FieldOffset(0x08)] internal uint Type;
        [FieldOffset(0x0C)] internal byte Opacity;
        [FieldOffset(0x10)] internal uint Unknown10;
    }
    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    internal struct FrFrameOrderedEntry
    {
        [FieldOffset(0x00)] internal int OrderingKey;
        [FieldOffset(0x08)] internal nint Model;
    }
}
