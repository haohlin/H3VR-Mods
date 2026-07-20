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
    public void Unity_build_waits_for_its_success_marker_and_package_before_evaluating_retry()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var buildStart = pipeline.IndexOf("function Invoke-UnityBuild", StringComparison.Ordinal);
        var buildEnd = pipeline.IndexOf("function Get-GunGameStagingPath", buildStart, StringComparison.Ordinal);

        Assert.True(buildStart >= 0 && buildEnd > buildStart,
            "Pipeline must expose the Unity build implementation.");
        var build = pipeline[buildStart..buildEnd];

        Assert.Contains("function Wait-ForUnityBuildOutput", pipeline, StringComparison.Ordinal);
        Assert.Contains("$buildCompleted = Wait-ForUnityBuildOutput", build,
            StringComparison.Ordinal);
        Assert.Contains("if ($process.ExitCode -ne 0 -and -not $buildCompleted)", build,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Wait-ForUnityProjectBatchWorker -ProjectRoot $projectRoot", build,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Unity_source_sync_is_guarded_and_available_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var prepareStart = pipeline.IndexOf("function Prepare-UnityProjectSourceSync", StringComparison.Ordinal);
        var syncStart = pipeline.IndexOf("function Sync-UnityProjectSource", StringComparison.Ordinal);
        var prepareEnd = syncStart;
        var syncEnd = syncStart < 0
            ? -1
            : pipeline.IndexOf("function Invoke-DotNetBuild", syncStart, StringComparison.Ordinal);

        Assert.True(prepareStart >= 0 && prepareEnd > prepareStart,
            "Pipeline must prepare an atomic Unity-source upload before synchronization.");
        Assert.True(syncStart >= 0 && syncEnd > syncStart,
            "Pipeline must provide a guarded Unity-source synchronization action.");
        var prepare = pipeline[prepareStart..prepareEnd];
        var sync = pipeline[syncStart..syncEnd];

        Assert.Contains("'PrepareUnitySourceSync'", pipeline, StringComparison.Ordinal);
        Assert.Contains("'SyncUnitySource'", pipeline, StringComparison.Ordinal);
        Assert.Contains("PrepareUnitySourceSync", wrapper, StringComparison.Ordinal);
        Assert.Contains("SyncUnitySource", wrapper, StringComparison.Ordinal);
        Assert.Contains("PrepareUnitySourceSync requires -Query <branch>.", prepare, StringComparison.Ordinal);
        Assert.Contains("NightForcePlus-source.zip", prepare, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", prepare, StringComparison.Ordinal);
        Assert.Contains("SyncUnitySource requires -Query <branch>.", sync, StringComparison.Ordinal);
        Assert.Contains("Get-UnityProjectRoot", sync, StringComparison.Ordinal);
        Assert.Contains("Join-Path $projectRoot 'Assets\\Projects'", sync, StringComparison.Ordinal);
        Assert.Contains("$stagingRoot = Join-Path (Join-Path $BuildRoot 'staging') 'unity-source-sync'", sync, StringComparison.Ordinal);
        Assert.Contains("NightForcePlus-source.zip", sync, StringComparison.Ordinal);
        Assert.Contains("Expand-Archive", sync, StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $sourceModRoot", sync, StringComparison.Ordinal);
        Assert.Contains("Get-FileSha256", sync, StringComparison.Ordinal);
        Assert.DoesNotContain("reset --hard", sync, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_item_id_audit_is_read_only_and_available_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var fileSearchStart = pipeline.IndexOf("function Test-FileContainsUtf8Text", StringComparison.Ordinal);
        var auditStart = pipeline.IndexOf("function Find-InstalledItemId", StringComparison.Ordinal);
        var fileSearchEnd = auditStart;
        var auditEnd = pipeline.IndexOf("function Assert-RemoteVersionIsNew", auditStart, StringComparison.Ordinal);
        Assert.True(fileSearchStart >= 0 && fileSearchEnd > fileSearchStart,
            "Pipeline must use a bounded managed file-content search for ItemID auditing.");
        Assert.True(auditStart >= 0, "Pipeline must expose a read-only ItemID audit.");
        Assert.True(auditEnd > auditStart, "Pipeline must dispatch the ItemID audit action.");
        var fileSearch = pipeline[fileSearchStart..fileSearchEnd];
        var audit = pipeline[auditStart..auditEnd];

        Assert.Contains("'AuditItemId'", pipeline, StringComparison.Ordinal);
        Assert.Contains("AuditItemId", wrapper, StringComparison.Ordinal);
        Assert.Contains("AuditItemId requires -Query <ItemID>.", audit, StringComparison.Ordinal);
        Assert.Contains("$EnvironmentConfig.r2modman.pluginsRoot", audit, StringComparison.Ordinal);
        Assert.Contains("Auditing $($packageDirectories.Count) candidate package(s)", audit, StringComparison.Ordinal);
        Assert.Contains("ItemID audit found $($auditMatches.Count) matching package(s)", audit, StringComparison.Ordinal);
        Assert.Contains("Write-Host \"ItemID match: $($match.Package) [$($match.Files)]\"", audit, StringComparison.Ordinal);
        Assert.Contains("foreach ($match in ($auditMatches | Sort-Object Package))", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("Format-Table", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", audit, StringComparison.Ordinal);
        Assert.Contains("[IO.File]::ReadAllText", fileSearch, StringComparison.Ordinal);
        Assert.DoesNotContain("for ($start", fileSearch, StringComparison.Ordinal);
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
