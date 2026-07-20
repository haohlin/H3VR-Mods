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
        Assert.Contains("function Get-ModProjectDirectory", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("$ModConfig.projectDir", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Unity_pipeline_accepts_explicit_private_config_overrides()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));

        Assert.Contains("[string]$EnvironmentConfigPath", pipeline, StringComparison.Ordinal);
        Assert.Contains("[string]$ModsConfigPath", pipeline, StringComparison.Ordinal);
        Assert.Contains("function Resolve-PipelineConfigPath", pipeline, StringComparison.Ordinal);
        Assert.Contains("Resolve-PipelineConfigPath -Path $EnvironmentConfigPath", pipeline, StringComparison.Ordinal);
        Assert.Contains("Resolve-PipelineConfigPath -Path $ModsConfigPath", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_item_id_audit_is_read_only_and_available_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var auditStart = pipeline.IndexOf("function Find-InstalledItemId", StringComparison.Ordinal);
        var auditEnd = pipeline.IndexOf("function Assert-RemoteVersionIsNew", auditStart, StringComparison.Ordinal);
        Assert.True(auditStart >= 0, "Pipeline must expose a read-only ItemID audit.");
        Assert.True(auditEnd > auditStart, "Pipeline must dispatch the ItemID audit action.");
        var audit = pipeline[auditStart..auditEnd];

        Assert.Contains("'AuditItemId'", pipeline, StringComparison.Ordinal);
        Assert.Contains("AuditItemId", wrapper, StringComparison.Ordinal);
        Assert.Contains("AuditItemId requires -Query <ItemID>.", audit, StringComparison.Ordinal);
        Assert.Contains("$EnvironmentConfig.r2modman.pluginsRoot", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", audit, StringComparison.Ordinal);
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
