using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x10)]
internal struct FrCacheOperation
{
    [FieldOffset(0x00)] internal uint Type;
    [FieldOffset(0x04)] internal uint IndexOrModelStart;
    [FieldOffset(0x08)] internal uint ModelCount;
    [FieldOffset(0x0C)] internal uint CacheFrameIndex;
}
[StructLayout(LayoutKind.Explicit, Size = 0x20)]
internal struct FrCacheModelBuffer
{
    [FieldOffset(0x00)] internal uint Unknown00;
    [FieldOffset(0x02)] internal ushort Unknown02;
    [FieldOffset(0x08)] internal nint Models;
    [FieldOffset(0x10)] internal uint Capacity;
    [FieldOffset(0x14)] internal uint Count;
    [FieldOffset(0x18)] internal uint Unknown18;
}
