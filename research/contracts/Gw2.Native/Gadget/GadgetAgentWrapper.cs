using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit)]
internal struct GadgetAgentWrapper
{
    [FieldOffset(0x000)] internal Unknown000Storage Unknown000;
    [StructLayout(LayoutKind.Sequential, Size = 0x1F8)] internal struct Unknown000Storage { }
    [FieldOffset(0x1F8)] internal uint Flags1F8;
    [FieldOffset(0x1FC)] internal int Unknown1FC;
    [FieldOffset(0x200)] internal uint Flags200;
    [FieldOffset(0x204)] internal int Unknown204;
    [FieldOffset(0x208)] internal GadgetType GadgetKind;
    [FieldOffset(0x20C)] internal Unknown20CStorage Unknown20C;
    [StructLayout(LayoutKind.Sequential, Size = 0x14)] internal struct Unknown20CStorage { }
    [FieldOffset(0x220)] internal nint Health;
    [FieldOffset(0x228)] internal Unknown228Storage Unknown228;
    [StructLayout(LayoutKind.Sequential, Size = 0x2B8)] internal struct Unknown228Storage { }
    [FieldOffset(0x4E0)] internal nint Unknown4E0Vtable;
    [FieldOffset(0x4E8)] internal nint Unknown4E8Vtable;
    [FieldOffset(0x4EC)] internal ResourceNodeType ResourceKind;
    // The master wrapper keeps one declaration at +0x4F0. The separate GdCliGadget overlay
    // carries the independently recovered scalar Flags interpretation for that native view.
    [FieldOffset(0x4F0)] internal nint Unknown4F0Vtable;
    [FieldOffset(0x4F8)] internal long Unknown4F8;
}
