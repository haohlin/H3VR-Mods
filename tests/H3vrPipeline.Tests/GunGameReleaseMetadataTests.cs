using System.Text.Json;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class GunGameReleaseMetadataTests
{
    private const string ModdedProfileRefreshNotice =
        "*** Modded profiles generate in background; depends on mod count. Reload GunGame to show them. ***";

    private const string CanonicalShortDescription =
        "GunGame, supercharged: 661 vanilla firearms with scopes, plus modded guns and custom Sosigs.";

    [Fact]
    public void Release_metadata_keeps_the_stable_listing_description_and_player_guide()
    {
        var packageRoot = Path.Combine(
            FindRepositoryRoot(),
            "GunGameProgressions",
            "Thunderstore",
            "HLin_Mods-GunGameProgression");

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(packageRoot, "manifest.json")));
        var root = manifest.RootElement;

        Assert.Equal("1.4.1", root.GetProperty("version_number").GetString());
        Assert.Equal(
            ModdedProfileRefreshNotice + " " + CanonicalShortDescription,
            root.GetProperty("description").GetString());

        var branding = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "GunGameProgressions",
            "BRANDING.md"));
        Assert.Contains("## Canonical short description\n\n" + CanonicalShortDescription, branding.ReplaceLineEndings("\n"));
        Assert.Contains("Do not reword or replace this description without explicit product approval.", branding);

        var readme = File.ReadAllText(Path.Combine(packageRoot, "README.md"));
        Assert.Contains("## Choose a Pool", readme);
        Assert.Contains("> **Modded profiles generate in the background; time depends on mod count. Reload the GunGame map to show them.**", readme);
        Assert.Contains("Runtime 02 - Modded Rot", readme);
        Assert.Contains("## Enemy Pacing", readme);
        Assert.Contains("## Compatible Loadouts", readme);
        Assert.Contains("## Your Enabled Content", readme);
        Assert.Contains("versioned vanilla metadata snapshot", readme);

        var changelog = File.ReadAllText(Path.Combine(packageRoot, "CHANGELOG.md"));
        Assert.Contains("## 1.4.1", changelog);

        var exporterProject = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "GunGameProgressions",
            "MetadataExporter",
            "GunGameProgressionsMetadataExporter.csproj"));
        Assert.Contains("<Version>1.4.1</Version>", exporterProject);

        var exporterSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "GunGameProgressions",
            "MetadataExporter",
            "src",
            "Plugin.cs"));
        Assert.Contains("GunGame Progressions Metadata Exporter\", \"1.4.1\"", exporterSource);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "GunGameProgressions")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the H3VR-Mods repository root.");
    }
}
