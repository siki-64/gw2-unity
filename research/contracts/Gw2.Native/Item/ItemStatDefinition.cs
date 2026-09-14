using System.Runtime.InteropServices;

namespace Gw2.Native;

[StructLayout(LayoutKind.Explicit)]
internal struct ItemStatDefinition
{
    [FieldOffset(0x00)] internal Unknown000Storage Unknown000;
    [StructLayout(LayoutKind.Sequential, Size = 0x28)] internal struct Unknown000Storage { }
    [FieldOffset(0x28)] internal uint Id;
}
