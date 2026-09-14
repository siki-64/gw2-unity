using System.Runtime.InteropServices;

namespace Gw2.Native;

// Native content-definition context.
[StructLayout(LayoutKind.Explicit, Size = 0x58A8)]
internal struct CnContext
{
    // Build 205.780 content lookup surface.
    internal const int FindByContentKeyVtableOffset = 0x68;
    internal const int FindByIdVtableOffset = 0x70;

    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x008)] internal nint ContentData0;
    [FieldOffset(0x010)] internal nint ContentData1;
}
