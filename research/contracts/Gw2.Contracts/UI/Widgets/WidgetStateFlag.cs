namespace Gw2.Contracts;

[Flags]
internal enum WidgetStateFlag : uint
{
    // Tested by WidgetVisible.
    Visible = 0x200,
    // Set when the camera-to-widget ray is blocked.
    Occluded = 0x1000
}

internal static class WidgetStateMasks
{
    internal const uint VisibilityRelevant = 0x41202;
}
