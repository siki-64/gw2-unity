using System.Runtime.InteropServices;

namespace Gw2.Native;

// Recovered from the constructor, tick, and smoothing routine; this is native
// storage, not an addon implementation of the presentation policy.
[StructLayout(LayoutKind.Explicit, Size = 0x14)]
internal struct InfoBarPresentationSmoother
{
    [FieldOffset(0x00)] internal float SmoothTime;
    [FieldOffset(0x04)] internal float Current;
    [FieldOffset(0x08)] internal float Target;
    [FieldOffset(0x0C)] internal float Velocity;
    [FieldOffset(0x10)] internal float SnapTolerance;
}
