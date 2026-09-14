using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal struct CmbtCliBreakBar
{
    [FieldOffset(0x040)] internal BreakbarState State;
    // Native consumer scales this value by 100 for presentation.
    [FieldOffset(0x044)] internal float NormalizedValue;
}
