using Xunit;

namespace H3vrPipeline.Tests;

public sealed class HeadlessAssetInspectionTests
{
    [Fact]
    public void Inspector_is_versioned_and_cleanup_is_explicit()
    {
        foreach (var relativePath in new[]
                 {
                     "tools/H3VRAssetInspector/inspect_assets.py",
                     "tools/H3VRAssetInspector/export_private_asset_rip.ps1",
                     "tools/H3VRAssetInspector/requirements.txt",
                     "tools/H3VRAssetInspector/H3VRAssetInspectionBatch.cs",
                     "tools/H3VRAssetInspector/tests/test_inspect_assets.py",
                     "skills/h3vr-remote-development/references/headless-unity-asset-inspection.md",
                 })
        {
            Assert.True(File.Exists(Path.Combine(RepositoryRoot, relativePath)), "Missing: " + relativePath);
        }

        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        Assert.Contains("h3vr-asset-inspector", pipeline, StringComparison.Ordinal);
        Assert.Contains("H3VRAssetInspectionBatch.cs", pipeline, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath $scratchDirectory -Recurse -Force", pipeline, StringComparison.Ordinal);
        Assert.Contains("Refusing to overwrite", pipeline, StringComparison.Ordinal);
        Assert.Contains("if ((Test-Path -LiteralPath $bootstrapPath) -or (Test-Path -LiteralPath $bootstrapMetaPath))", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("Test-Path -LiteralPath $bootstrapPath -or", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Private_asset_rip_driver_is_headless_atomic_and_non_destructive()
    {
        var driver = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "tools",
            "H3VRAssetInspector",
            "export_private_asset_rip.ps1"));

        Assert.Contains("--headless=true", driver, StringComparison.Ordinal);
        Assert.Contains("$baseUri/LoadFolder", driver, StringComparison.Ordinal);
        Assert.Contains("$baseUri/LoadFile", driver, StringComparison.Ordinal);
        Assert.Contains("$baseUri/Export/UnityProject", driver, StringComparison.Ordinal);
        Assert.Contains("Refusing to overwrite existing AssetRipper export", driver, StringComparison.Ordinal);
        Assert.Contains("Move-Item -LiteralPath $stagingPath -Destination $outputPath", driver, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item -LiteralPath $stagingPath", driver, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_workflow_runs_portable_inspector_tests()
    {
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "verify.yml"));

        Assert.Contains("Run headless asset-inspector portable tests", workflow, StringComparison.Ordinal);
        Assert.Contains("python tools/H3VRAssetInspector/tests/test_inspect_assets.py", workflow, StringComparison.Ordinal);
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
