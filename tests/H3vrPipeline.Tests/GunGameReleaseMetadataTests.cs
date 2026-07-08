using System.Text.Json;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class GunGameReleaseMetadataTests
{
    [Fact]
    public void Release_metadata_advertises_the_verified_runtime_catalog()
    {
        var packageRoot = Path.Combine(
            FindRepositoryRoot(),
            "GunGameProgressions",
            "Thunderstore",
            "HLin_Mods-GunGameProgression");

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(packageRoot, "manifest.json")));
        var root = manifest.RootElement;

        Assert.Equal("1.3.4", root.GetProperty("version_number").GetString());
        Assert.Equal(
            "GunGame, supercharged: 615 vanilla firearms, 372 magazines, 10 clips, and 171 cartridges, plus all supported active modded guns and custom Sosigs. Fast runtime pools rebuild each launch with compatible loadouts.",
            root.GetProperty("description").GetString());

        var readme = File.ReadAllText(Path.Combine(packageRoot, "README.md"));
        Assert.Contains("615 supported vanilla firearms", readme);
        Assert.Contains("372 magazines, 10 clips, and 171 cartridges", readme);
        Assert.Contains("all supported active modded guns and custom Sosigs", readme);

        var changelog = File.ReadAllText(Path.Combine(packageRoot, "CHANGELOG.md"));
        Assert.Contains("## 1.3.4", changelog);

        var exporterProject = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "GunGameProgressions",
            "MetadataExporter",
            "GunGameProgressionsMetadataExporter.csproj"));
        Assert.Contains("<Version>1.3.4</Version>", exporterProject);
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
