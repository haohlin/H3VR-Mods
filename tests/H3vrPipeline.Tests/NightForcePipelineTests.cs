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
            "[NightForcePlusRuntime] MeatKit package built from exact profile.",
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
        Assert.Contains("Remove-Item -LiteralPath $logPath -Force -ErrorAction Stop", build,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Unity_build_status_reports_current_marker_and_source_package_without_exposing_private_paths()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var statusStart = pipeline.IndexOf("function Get-UnityBuildStatus", StringComparison.Ordinal);
        var statusEnd = pipeline.IndexOf("function Get-GunGameStagingPath", statusStart, StringComparison.Ordinal);

        Assert.True(statusStart >= 0 && statusEnd > statusStart,
            "Pipeline must expose a read-only Unity build status action.");
        var status = pipeline[statusStart..statusEnd];

        Assert.Contains("'UnityBuildStatus'", pipeline, StringComparison.Ordinal);
        Assert.Contains("UnityBuildStatus", wrapper, StringComparison.Ordinal);
        Assert.Contains("Get-UnityPackageSourcePath", status, StringComparison.Ordinal);
        Assert.Contains("Select-String -LiteralPath $logPath", status, StringComparison.Ordinal);
        Assert.Contains("Get-FileSha256", status, StringComparison.Ordinal);
        Assert.Contains("Build failure marker present:", status, StringComparison.Ordinal);
        Assert.Contains("NightForce runtime diagnostics:", status, StringComparison.Ordinal);
        Assert.Contains("Last NightForce runtime diagnostic:", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Package path", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Log path", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", status, StringComparison.Ordinal);
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

    [Fact]
    public void Private_asset_archive_status_is_read_only_and_available_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var statusStart = pipeline.IndexOf("function Get-PrivateAssetArchiveStatus", StringComparison.Ordinal);
        var statusEnd = pipeline.IndexOf("function Assert-RemoteVersionIsNew", statusStart, StringComparison.Ordinal);

        Assert.True(statusStart >= 0 && statusEnd > statusStart,
            "Pipeline must expose a read-only private asset-archive status action.");
        var status = pipeline[statusStart..statusEnd];

        Assert.Contains("'AssetRipStatus'", pipeline, StringComparison.Ordinal);
        Assert.Contains("AssetRipStatus", wrapper, StringComparison.Ordinal);
        Assert.Contains("H3VR_PRIVATE_ASSET_LAB", status, StringComparison.Ordinal);
        Assert.Contains("H3VRFull-export-files-*.sha256.tsv", status, StringComparison.Ordinal);
        Assert.Contains("Mesh", status, StringComparison.Ordinal);
        Assert.Contains("Material", status, StringComparison.Ordinal);
        Assert.Contains("Texture2D", status, StringComparison.Ordinal);
        Assert.Contains("Shader", status, StringComparison.Ordinal);
        Assert.Contains("Prefab", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Write-Host $assetLab", status, StringComparison.Ordinal);
    }

    [Fact]
    public void Private_asset_lab_configuration_is_forwarded_without_entering_repository_files()
    {
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));

        Assert.Contains("H3VR_PRIVATE_ASSET_LAB", wrapper, StringComparison.Ordinal);
        Assert.Contains("asset_lab_prefix", wrapper, StringComparison.Ordinal);
        Assert.Contains("case \"$H3VR_PRIVATE_ASSET_LAB\"", wrapper, StringComparison.Ordinal);
        Assert.DoesNotContain("H3VR-PrivateScopeLab", wrapper, StringComparison.Ordinal);
    }

    [Fact]
    public void Private_asset_archive_search_is_read_only_and_available_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var searchStart = pipeline.IndexOf("function Find-PrivateAssetRip", StringComparison.Ordinal);
        var searchEnd = pipeline.IndexOf("function Assert-RemoteVersionIsNew", searchStart, StringComparison.Ordinal);

        Assert.True(searchStart >= 0 && searchEnd > searchStart,
            "Pipeline must expose a read-only private asset-archive search action.");
        var search = pipeline[searchStart..searchEnd];

        Assert.Contains("'FindAssetRip'", pipeline, StringComparison.Ordinal);
        Assert.Contains("FindAssetRip", wrapper, StringComparison.Ordinal);
        Assert.Contains("FindAssetRip requires -Query <text>.", search, StringComparison.Ordinal);
        Assert.Contains("Get-Content -LiteralPath $manifest.FullName", search, StringComparison.Ordinal);
        Assert.Contains("Asset match:", search, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", search, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", search, StringComparison.Ordinal);
    }

    [Fact]
    public void Private_asset_graph_inspection_is_read_only_and_available_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var graphStart = pipeline.IndexOf("function Get-PrivateAssetRipGraph", StringComparison.Ordinal);
        var graphEnd = pipeline.IndexOf("function Assert-RemoteVersionIsNew", graphStart, StringComparison.Ordinal);

        Assert.True(graphStart >= 0 && graphEnd > graphStart,
            "Pipeline must expose a read-only private asset dependency-graph action.");
        var graph = pipeline[graphStart..graphEnd];

        Assert.Contains("'InspectAssetRip'", pipeline, StringComparison.Ordinal);
        Assert.Contains("InspectAssetRip", wrapper, StringComparison.Ordinal);
        Assert.Contains("InspectAssetRip requires -Query <prefab name>.", graph, StringComparison.Ordinal);
        Assert.Contains("guid:", graph, StringComparison.Ordinal);
        Assert.Contains("Dependency:", graph, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", graph, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", graph, StringComparison.Ordinal);
    }

    [Fact]
    public void Unity_asset_rip_status_is_read_only_and_available_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var statusStart = pipeline.IndexOf("function Get-UnityAssetRipImportStatus", StringComparison.Ordinal);
        var statusEnd = pipeline.IndexOf("function Assert-RemoteVersionIsNew", statusStart, StringComparison.Ordinal);

        Assert.True(statusStart >= 0 && statusEnd > statusStart,
            "Pipeline must expose a read-only Unity asset-rip import status action.");
        var status = pipeline[statusStart..statusEnd];

        Assert.Contains("'UnityAssetRipStatus'", pipeline, StringComparison.Ordinal);
        Assert.Contains("UnityAssetRipStatus", wrapper, StringComparison.Ordinal);
        Assert.Contains("Assets\\main-game\\Assets", status, StringComparison.Ordinal);
        Assert.Contains("LT3x9Scope.prefab", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", status, StringComparison.Ordinal);
    }

    [Fact]
    public void Unity_vanilla_scope_import_smoke_test_is_exposed_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var actionStart = pipeline.IndexOf("function Invoke-UnityVanillaScopeImportSmokeTest", StringComparison.Ordinal);
        var actionEnd = pipeline.IndexOf("function Get-PrivateAssetArchiveStatus", actionStart, StringComparison.Ordinal);

        Assert.True(actionStart >= 0 && actionEnd > actionStart,
            "Pipeline must expose a Unity batch smoke test for the private vanilla scope importer.");
        var action = pipeline[actionStart..actionEnd];

        Assert.Contains("'UnityVanillaImportSmokeTest'", pipeline, StringComparison.Ordinal);
        Assert.Contains("UnityVanillaImportSmokeTest", wrapper, StringComparison.Ordinal);
        Assert.Contains("HLin_Mods.PrivateTools.VanillaScopeReferenceImporter.RunProjectSmokeTest", action, StringComparison.Ordinal);
        Assert.Contains("[VanillaScopeReferenceImporter] PASS:", action, StringComparison.Ordinal);
        Assert.Contains("Windows Unity project is open", action, StringComparison.Ordinal);
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
