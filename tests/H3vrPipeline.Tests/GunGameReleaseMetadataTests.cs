using System.Text.Json;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class GunGameReleaseMetadataTests
{
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

        Assert.Equal("1.3.7", root.GetProperty("version_number").GetString());
        Assert.Equal(
            "⚠ Modded profiles generate in the background (1-5 min). Reload GunGame to show them.",
            root.GetProperty("description").GetString());

        var readme = File.ReadAllText(Path.Combine(packageRoot, "README.md"));
        Assert.Contains("## Choose a Pool", readme);
        Assert.Contains("Runtime 02 - Modded Rot", readme);
        Assert.Contains("## Enemy Pacing", readme);
        Assert.Contains("## Compatible Loadouts", readme);
        Assert.Contains("## Your Enabled Content", readme);
        Assert.Contains("versioned vanilla metadata snapshot", readme);

        var changelog = File.ReadAllText(Path.Combine(packageRoot, "CHANGELOG.md"));
        Assert.Contains("## 1.3.7", changelog);

        var exporterProject = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "GunGameProgressions",
            "MetadataExporter",
            "GunGameProgressionsMetadataExporter.csproj"));
        Assert.Contains("<Version>1.3.7</Version>", exporterProject);

        var exporterSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "GunGameProgressions",
            "MetadataExporter",
            "src",
            "Plugin.cs"));
        Assert.Contains("GunGame Progressions Metadata Exporter\", \"1.3.7\"", exporterSource);
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
