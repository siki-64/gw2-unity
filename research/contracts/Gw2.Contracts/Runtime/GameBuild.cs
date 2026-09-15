namespace Gw2.Contracts;

public static class GameBuild
{
    // No build is admitted to the runtime support set until its layouts are revalidated.
    public const uint SupportedGameBuild = 0;

    public static bool IsSupported(uint gameBuild) =>
        gameBuild == SupportedGameBuild;
}
