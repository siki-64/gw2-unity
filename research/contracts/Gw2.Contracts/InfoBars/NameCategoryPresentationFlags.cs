namespace Gw2.Contracts;

[Flags]
internal enum NameCategoryPresentationFlags : uint
{
    FriendlyPlayerDistanceBrightness = 1u << 3,
    PreserveRelationshipVariants = 1u << 14,
}
