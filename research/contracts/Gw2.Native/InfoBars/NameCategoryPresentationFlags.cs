namespace Gw2.Native;

[Flags]
internal enum NameCategoryPresentationFlags : uint
{
    FriendlyPlayerDistanceBrightness = 1u << 3,
    PreserveRelationshipVariants = 1u << 14,
}
