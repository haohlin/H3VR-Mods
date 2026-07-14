using System.Text.Json;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class BubbleLevelPipelineTests
{
    [Fact]
    public void BubbleLevel_has_a_complete_unity_package_descriptor()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "build", "mods.json")));
        var bubbleLevel = document.RootElement
            .GetProperty("mods")
            .GetProperty("BubbleLevel");

        Assert.Equal("unity", bubbleLevel.GetProperty("kind").GetString());
        Assert.Equal("BubbleLevelSet", bubbleLevel.GetProperty("packageName").GetString());
        Assert.Equal("HLin_Mods-BubbleLevelSet", bubbleLevel.GetProperty("deploymentFolder").GetString());
        Assert.Equal("legacy-flat", bubbleLevel.GetProperty("layout").GetString());
        Assert.Equal(
            "Assets\\Projects\\BubbleLevel\\BuildProfile-BubbleLevel.asset",
            bubbleLevel.GetProperty("versionProfileRelativePath").GetString());
        Assert.Contains(
            "{version}",
            bubbleLevel.GetProperty("packageRelativePath").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(
            "HLin_Mods.BubbleLevelSet.Editor.BubbleLevelRuntimeTests.BuildBubbleLevelPackage",
            bubbleLevel.GetProperty("unityBuildMethod").GetString());
    }

    [Fact]
    public void Unity_wrapper_can_build_or_reuse_an_existing_BubbleLevel_package()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));

        Assert.Contains("'BubbleLevel'", pipeline, StringComparison.Ordinal);
        Assert.Contains("ReuseExistingUnityPackage", pipeline, StringComparison.Ordinal);
        Assert.Contains("Invoke-UnityBuild", pipeline, StringComparison.Ordinal);
        Assert.Contains("New-UnityPackage", pipeline, StringComparison.Ordinal);
        Assert.Contains("Unsupported build kind", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Unity_wrapper_reads_an_indented_MeatKit_profile_version()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));

        Assert.Contains("(?m)^\\s*Version:\\s*(?<version>\\S+)\\s*$", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Unity_wrapper_retries_after_script_compilation_and_requires_a_fresh_MeatKit_build()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));

        Assert.Contains("$attempt -le 2", pipeline, StringComparison.Ordinal);
        Assert.Contains("MeatKit package built from exact profile", pipeline, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath $packagePath", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Unity_paths_stay_private_environment_configuration()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "build", "environment.json")));
        var unity = document.RootElement.GetProperty("unity");

        Assert.Equal("%H3VR_UNITY_MEATKIT_PROJECT%", unity.GetProperty("meatKitProjectRoot").GetString());
        Assert.Equal("%H3VR_UNITY_EXECUTABLE%", unity.GetProperty("editorExecutable").GetString());
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
