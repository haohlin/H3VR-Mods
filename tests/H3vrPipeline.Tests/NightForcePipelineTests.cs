using System.Text.Json;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class NightForcePipelineTests
{
    [Fact]
    public void NightForcePlus_has_a_complete_unity_package_descriptor()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "build", "mods.json")));
        var nightForce = document.RootElement
            .GetProperty("mods")
            .GetProperty("NightForcePlus");

        Assert.Equal("unity", nightForce.GetProperty("kind").GetString());
        Assert.Equal("NightForcePlus", nightForce.GetProperty("packageName").GetString());
        Assert.Equal("HLin_Mods-NightForcePlus", nightForce.GetProperty("deploymentFolder").GetString());
        Assert.Equal("legacy-flat", nightForce.GetProperty("layout").GetString());
        Assert.Equal(
            "Assets\\Projects\\NightForcePlus\\Profile-NightForcePlus.asset",
            nightForce.GetProperty("versionProfileRelativePath").GetString());
        Assert.Equal(
            "AssetBundles\\NightForcePlus\\{version}\\HLin_Mods-NightForcePlus-{version}.zip",
            nightForce.GetProperty("packageRelativePath").GetString());
        Assert.Equal(
            "HLin_Mods.BubbleLevelScope.Editor.NightForcePlusRuntimeTests.BuildNightForcePlusPackage",
            nightForce.GetProperty("unityBuildMethod").GetString());
        Assert.Equal(
            "[NightForcePlusRuntime] MeatKit package built from exact profile:",
            nightForce.GetProperty("unityBuildSuccessMarker").GetString());
    }

    [Fact]
    public void Unity_wrapper_accepts_NightForcePlus_commands()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));

        Assert.Contains("'NightForcePlus'", pipeline, StringComparison.Ordinal);
        Assert.Contains("Invoke-UnityBuild", pipeline, StringComparison.Ordinal);
        Assert.Contains("New-UnityPackage", pipeline, StringComparison.Ordinal);
    }

    private static string RepositoryRoot
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "build", "mods.json")))
                    return current.FullName;

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the H3VR-Mods repository root.");
        }
    }
}
