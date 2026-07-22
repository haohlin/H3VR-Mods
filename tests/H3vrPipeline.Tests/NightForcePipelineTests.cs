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
    public void Local_vanilla_scope_candidates_have_a_separate_unity_package_descriptor()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "build", "mods.json")));
        var candidates = document.RootElement
            .GetProperty("mods")
            .GetProperty("VanillaScopeCandidatesLocal");

        Assert.Equal("unity", candidates.GetProperty("kind").GetString());
        Assert.Equal("LocalVanillaScopeCandidates", candidates.GetProperty("packageName").GetString());
        Assert.Equal("HLin_Mods-LocalVanillaScopeCandidates", candidates.GetProperty("deploymentFolder").GetString());
        Assert.Equal("legacy-flat", candidates.GetProperty("layout").GetString());
        Assert.Equal(
            "Assets\\Projects\\PrivateVanillaRuntimeCandidates\\Profile-LocalVanillaScopeCandidates.asset",
            candidates.GetProperty("versionProfileRelativePath").GetString());
        Assert.Equal(
            "AssetBundles\\LocalVanillaScopeCandidates\\{version}\\HLin_Mods-LocalVanillaScopeCandidates-{version}.zip",
            candidates.GetProperty("packageRelativePath").GetString());
        Assert.Equal(
            "HLin_Mods.PrivateTools.VanillaScopeReferenceImporter.BuildLocalRuntimeCandidatePackage",
            candidates.GetProperty("unityBuildMethod").GetString());
        Assert.Equal(
            "[VanillaScopeLocalRuntime] PASS: MeatKit package built for LocalRecoveredST6TBlack, LocalRecoveredLT3x9, and LocalRecoveredEVU1x10.",
            candidates.GetProperty("unityBuildSuccessMarker").GetString());
        var requiredEntries = candidates.GetProperty("unityRequiredPackageEntries")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        Assert.Equal(new[]
        {
            "hlin-localrecoveredst6tblack",
            "hlin-localrecoveredlt3x9",
            "hlin-localrecoveredevu1x10"
        }, requiredEntries);
    }

    [Fact]
    public void Unity_deployment_audit_is_read_only_and_available_through_the_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var auditStart = pipeline.IndexOf("function Get-UnityDeploymentAudit", StringComparison.Ordinal);
        var auditEnd = pipeline.IndexOf("function Get-PrivateAssetArchiveStatus", auditStart, StringComparison.Ordinal);

        Assert.True(auditStart >= 0 && auditEnd > auditStart,
            "Pipeline must expose a bounded Unity deployment audit before asset archive helpers.");
        var audit = pipeline[auditStart..auditEnd];

        Assert.Contains("'AuditUnityDeployment'", pipeline, StringComparison.Ordinal);
        Assert.Contains("AuditUnityDeployment", wrapper, StringComparison.Ordinal);
        Assert.Contains("Assert-UnityPackageRequiredEntries", audit, StringComparison.Ordinal);
        Assert.Contains("Payload match:", audit, StringComparison.Ordinal);
        Assert.Contains("Bundle entries:", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", audit, StringComparison.Ordinal);
    }

    [Fact]
    public void Deploy_uses_a_transactional_manager_owned_r2modman_install()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var installStart = pipeline.IndexOf("function Install-R2modmanManagedPackage", StringComparison.Ordinal);
        var auditStart = pipeline.IndexOf("function Get-R2modmanManagedDeploymentAudit", StringComparison.Ordinal);
        var auditEnd = pipeline.IndexOf("function Invoke-WindowsShutdown", auditStart, StringComparison.Ordinal);

        Assert.True(installStart >= 0 && auditStart > installStart && auditEnd > auditStart,
            "Pipeline must keep the manager install and audit helpers bounded before shutdown.");
        var install = pipeline[installStart..auditStart];
        var audit = pipeline[auditStart..auditEnd];

        Assert.Contains("Install-R2modmanManagedPackage -ModConfig $ModConfig -Package $package", pipeline,
            StringComparison.Ordinal);
        Assert.Contains("'AuditManagedDeployment'", pipeline, StringComparison.Ordinal);
        Assert.Contains("AuditManagedDeployment", wrapper, StringComparison.Ordinal);
        Assert.Contains("mm_v2_manifest.json", install, StringComparison.Ordinal);
        Assert.Contains("mods.yml", install, StringComparison.Ordinal);
        Assert.Contains("enabled: true", install, StringComparison.Ordinal);
        Assert.Contains("Assert-DirectoryPayloadMatch", install, StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $cacheTarget -Destination $cacheBackup", install, StringComparison.Ordinal);
        Assert.Contains("Manager payload match: True", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", audit, StringComparison.Ordinal);
    }

    [Fact]
    public void Unity_item_spawner_rule_requires_mod_content_metadata()
    {
        var developmentSkill = File.ReadAllText(Path.Combine(
            RepositoryRoot, ".codex", "skills", "h3vr-mod-development", "SKILL.md"));
        var remoteSkill = File.ReadAllText(Path.Combine(
            RepositoryRoot, "skills", "h3vr-remote-development", "SKILL.md"));

        foreach (var skill in new[] { developmentSkill, remoteSkill })
        {
            Assert.Contains("FVRObject.IsModContent: 1", skill, StringComparison.Ordinal);
            Assert.Contains("SpawnerEntry.IsModded: 1", skill, StringComparison.Ordinal);
            Assert.Contains("editor verifier", skill, StringComparison.Ordinal);
            Assert.Contains("Item Spawner", skill, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Local_vanilla_runtime_candidate_preparation_is_headless_safe_and_private()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var actionStart = pipeline.IndexOf("function Invoke-UnityVanillaRuntimeCandidatePreparation", StringComparison.Ordinal);
        var statusStart = pipeline.IndexOf("function Get-UnityVanillaRuntimeCandidateStatus", StringComparison.Ordinal);
        var statusEnd = pipeline.IndexOf("function Get-UnityVanillaPrefabImportStatus", statusStart, StringComparison.Ordinal);

        Assert.True(actionStart >= 0 && statusStart > actionStart && statusEnd > statusStart,
            "Pipeline must expose bounded local vanilla runtime preparation and status actions.");
        var action = pipeline[actionStart..statusStart];
        var status = pipeline[statusStart..statusEnd];

        Assert.Contains("'VanillaScopeCandidatesLocal'", pipeline, StringComparison.Ordinal);
        Assert.Contains("'UnityVanillaRuntimeCandidatePrepare'", pipeline, StringComparison.Ordinal);
        Assert.Contains("'UnityVanillaRuntimeCandidateStatus'", pipeline, StringComparison.Ordinal);
        Assert.Contains("UnityVanillaRuntimeCandidatePrepare", wrapper, StringComparison.Ordinal);
        Assert.Contains("UnityVanillaRuntimeCandidateStatus", wrapper, StringComparison.Ordinal);
        Assert.Contains("HLin_Mods.PrivateTools.VanillaScopeReferenceImporter.PrepareLocalRuntimeCandidates", action,
            StringComparison.Ordinal);
        Assert.Contains("[VanillaScopeLocalRuntime] PREPARED:", action, StringComparison.Ordinal);
        Assert.Contains("Windows Unity project is open", action, StringComparison.Ordinal);
        Assert.Contains("Wait-ForVanillaPrefabImporterResult", action, StringComparison.Ordinal);
        Assert.Contains("function Assert-UnityPackageRequiredEntries", pipeline, StringComparison.Ordinal);
        Assert.Contains("Assert-UnityPackageRequiredEntries -ModConfig $ModConfig -PackagePath $packagePath", pipeline,
            StringComparison.Ordinal);
        Assert.Contains("Preparation marker:", status, StringComparison.Ordinal);
        Assert.DoesNotContain("H3VR_PRIVATE_ASSET_LAB", action, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", action, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", action, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", status, StringComparison.Ordinal);
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
    public void Windows_shutdown_is_guarded_non_forced_and_available_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var actionStart = pipeline.IndexOf("function Invoke-WindowsShutdown", StringComparison.Ordinal);
        var actionEnd = pipeline.IndexOf("function Invoke-LogAction", actionStart, StringComparison.Ordinal);

        Assert.True(actionStart >= 0 && actionEnd > actionStart,
            "Pipeline must expose guarded Windows shutdown action.");
        var action = pipeline[actionStart..actionEnd];

        Assert.Contains("'ShutdownWindows'", pipeline, StringComparison.Ordinal);
        Assert.Contains("ShutdownWindows", wrapper, StringComparison.Ordinal);
        Assert.Contains("Get-UnityProjectRoot", action, StringComparison.Ordinal);
        Assert.Contains("Windows Unity project is open. Close it before shutdown.", action, StringComparison.Ordinal);
        Assert.Contains("shutdown.exe /s /t 60", action, StringComparison.Ordinal);
        Assert.Contains("shutdown.exe /a", action, StringComparison.Ordinal);
        Assert.DoesNotContain(" /f", action, StringComparison.Ordinal);
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
    public void Native_command_and_preflight_output_redact_private_paths()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var nativeStart = pipeline.IndexOf("function ConvertTo-PrivateSafeText", StringComparison.Ordinal);
        var preflightStart = pipeline.IndexOf("function Invoke-Preflight", StringComparison.Ordinal);
        var buildStart = pipeline.IndexOf("function Wait-ForUnityProjectBatchWorker", StringComparison.Ordinal);

        Assert.True(nativeStart >= 0 && preflightStart > nativeStart && buildStart > preflightStart,
            "Pipeline must redact native command and preflight diagnostics before Unity helpers.");
        var native = pipeline[nativeStart..preflightStart];
        var preflight = pipeline[preflightStart..buildStart];

        Assert.Contains("function ConvertTo-PrivateSafeText", native, StringComparison.Ordinal);
        Assert.Contains("& $Command 2>&1", native, StringComparison.Ordinal);
        Assert.Contains("Write-Host (ConvertTo-PrivateSafeText ([string]$line))", native, StringComparison.Ordinal);
        Assert.Contains("Repository: configured Windows checkout", preflight, StringComparison.Ordinal);
        Assert.DoesNotContain("Repository: $RepoRoot", preflight, StringComparison.Ordinal);
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
        Assert.Contains("Get-UnityPackageSourcePath -ModConfig $ModConfig -Version $Version", pipeline,
            StringComparison.Ordinal);
        Assert.Contains("-ModConfig $ModConfig -Version $version", build,
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
        Assert.Contains("$runtimeDiagnosticLines = @(", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Package path", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Log path", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", status, StringComparison.Ordinal);
    }

    [Fact]
    public void Saved_NightForce_prefab_status_is_private_path_safe_and_exposed_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var statusStart = pipeline.IndexOf("function Get-UnityNightForcePrefabStatus", StringComparison.Ordinal);
        var statusEnd = pipeline.IndexOf("function Get-GunGameStagingPath", statusStart, StringComparison.Ordinal);

        Assert.True(statusStart >= 0 && statusEnd > statusStart,
            "Pipeline must expose bounded saved-NightForce-prefab status.");
        var status = pipeline[statusStart..statusEnd];

        Assert.Contains("'UnityNightForcePrefabStatus'", pipeline, StringComparison.Ordinal);
        Assert.Contains("UnityNightForcePrefabStatus", wrapper, StringComparison.Ordinal);
        Assert.Contains("Assets\\Projects\\NightForcePlus\\NightForcePlus.prefab", status, StringComparison.Ordinal);
        Assert.Contains("Saved NightForcePlus prefab: present", status, StringComparison.Ordinal);
        Assert.Contains("Saved prefab SHA-256:", status, StringComparison.Ordinal);
        Assert.Contains("Unity editor:", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Write-Host $prefabPath", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", status, StringComparison.Ordinal);
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
        var auditEnd = pipeline.IndexOf("function Invoke-UnityVanillaScopeImportSmokeTest", auditStart, StringComparison.Ordinal);
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
    public void Private_asset_archive_root_discovery_reaches_the_current_export_layout()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var rootsStart = pipeline.IndexOf("function Get-PrivateAssetRipExportRoots", StringComparison.Ordinal);
        var rootsEnd = pipeline.IndexOf("function Resolve-PrivateAssetRipSourceFile", rootsStart, StringComparison.Ordinal);

        Assert.True(rootsStart >= 0 && rootsEnd > rootsStart,
            "Pipeline must discover materialized AssetRipper export roots.");
        var roots = pipeline[rootsStart..rootsEnd];

        Assert.Contains("$depth -le 4", roots, StringComparison.Ordinal);
        Assert.Contains("Join-Path $_ 'Assets'", roots, StringComparison.Ordinal);
        Assert.Contains("$allRoots", roots, StringComparison.Ordinal);
        Assert.Contains("return @($allRoots)", roots, StringComparison.Ordinal);
        Assert.DoesNotContain("return $roots", roots, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", roots, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", roots, StringComparison.Ordinal);
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
    public void Remote_wrapper_can_select_a_branch_worktree_without_exposing_its_path()
    {
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));

        Assert.Contains("H3VR_WINDOWS_WORKTREE_BRANCH", wrapper, StringComparison.Ordinal);
        Assert.Contains("git -C", wrapper, StringComparison.Ordinal);
        Assert.Contains("worktree list --porcelain", wrapper, StringComparison.Ordinal);
        Assert.Contains("Requested Windows worktree branch was not found.", wrapper, StringComparison.Ordinal);
        Assert.Contains("pipeline_repository", wrapper, StringComparison.Ordinal);
        Assert.DoesNotContain("H3VR-Mods-nightforce-runtime", wrapper, StringComparison.Ordinal);
        Assert.DoesNotContain("printf '%s\\n' \"$pipeline_repository\"", wrapper, StringComparison.Ordinal);
    }

    [Fact]
    public void Remote_worktree_execution_reuses_private_machine_environment_configuration()
    {
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));

        Assert.Contains("private_environment_config", wrapper, StringComparison.Ordinal);
        Assert.Contains("-EnvironmentConfigPath", wrapper, StringComparison.Ordinal);
        Assert.Contains("$H3VR_WINDOWS_REPOSITORY", wrapper, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", wrapper, StringComparison.Ordinal);
        Assert.DoesNotContain("H3VR-Mods-nightforce-runtime", wrapper, StringComparison.Ordinal);
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
        Assert.Contains("H3VR_PRIVATE_ASSET_LAB", status, StringComparison.Ordinal);
        Assert.Contains("exports\\H3VRFull\\AssetRipperProject\\ExportedProject\\Assets", status, StringComparison.Ordinal);
        Assert.Contains("ST6T*.prefab", status, StringComparison.Ordinal);
        Assert.DoesNotContain("main-game", status, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("Wait-ForUnityProjectBatchWorker -ProjectRoot $projectRoot", action, StringComparison.Ordinal);
        Assert.Contains("Start-Process -FilePath $unityConfig.editorExecutable -ArgumentList $arguments -PassThru", action, StringComparison.Ordinal);
        Assert.DoesNotContain("-ArgumentList $arguments -Wait -PassThru", action, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_private_vanilla_prefab_smoke_test_is_query_bound_and_exposed_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var actionStart = pipeline.IndexOf("function Invoke-UnityVanillaPrefabSmokeTest", StringComparison.Ordinal);
        Assert.True(actionStart >= 0,
            "Pipeline must expose a generic private vanilla-prefab smoke test.");
        var actionEnd = pipeline.IndexOf("function Get-UnityVanillaScopeImportStatus", actionStart, StringComparison.Ordinal);

        Assert.True(actionEnd > actionStart,
            "Pipeline must expose a generic private vanilla-prefab smoke test.");
        var action = pipeline[actionStart..actionEnd];

        Assert.Contains("'UnityVanillaPrefabSmokeTest'", pipeline, StringComparison.Ordinal);
        Assert.Contains("UnityVanillaPrefabSmokeTest", wrapper, StringComparison.Ordinal);
        Assert.Contains("UnityVanillaPrefabSmokeTest requires -Query <prefab name>.", action, StringComparison.Ordinal);
        Assert.Contains("H3VR_VANILLA_PREFAB_NAME", action, StringComparison.Ordinal);
        Assert.Contains("HLin_Mods.PrivateTools.VanillaScopeReferenceImporter.RunRequestedPrefabSmokeTest", action, StringComparison.Ordinal);
        Assert.Contains("'-projectPath', ('\"{0}\"' -f $projectRoot)", action, StringComparison.Ordinal);
        Assert.Contains("'-logFile', ('\"{0}\"' -f $logPath)", action, StringComparison.Ordinal);
        Assert.Contains("Wait-ForVanillaPrefabImporterResult", action, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", action, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", action, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_prefab_compare_action_and_status_are_headless_safe_and_exposed_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var actionStart = pipeline.IndexOf("function Invoke-UnityVanillaPrefabComparison", StringComparison.Ordinal);
        var statusStart = pipeline.IndexOf("function Get-UnityVanillaPrefabImportStatus", StringComparison.Ordinal);

        Assert.True(actionStart >= 0 && statusStart > actionStart,
            "Pipeline must expose a generic private prefab comparison action and status reader.");
        var action = pipeline[actionStart..statusStart];
        var statusEnd = pipeline.IndexOf("function Get-UnityVanillaScopeImportStatus", statusStart, StringComparison.Ordinal);
        Assert.True(statusEnd > statusStart,
            "Pipeline must expose a bounded generic private prefab import status reader.");
        var status = pipeline[statusStart..statusEnd];

        Assert.Contains("'UnityVanillaPrefabCompareNightForce'", pipeline, StringComparison.Ordinal);
        Assert.Contains("'UnityVanillaPrefabImportStatus'", pipeline, StringComparison.Ordinal);
        Assert.Contains("UnityVanillaPrefabCompareNightForce", wrapper, StringComparison.Ordinal);
        Assert.Contains("UnityVanillaPrefabImportStatus", wrapper, StringComparison.Ordinal);
        Assert.Contains("UnityVanillaPrefabCompareNightForce requires -Query <prefab name>.", action, StringComparison.Ordinal);
        Assert.Contains("HLin_Mods.PrivateTools.VanillaScopeReferenceImporter.RunRequestedPrefabComparisonAgainstNightForce", action, StringComparison.Ordinal);
        Assert.Contains("'-projectPath', ('\"{0}\"' -f $projectRoot)", action, StringComparison.Ordinal);
        Assert.Contains("'-logFile', ('\"{0}\"' -f $logPath)", action, StringComparison.Ordinal);
        Assert.Contains("Wait-ForVanillaPrefabImporterResult", action, StringComparison.Ordinal);
        Assert.Contains("vanilla-prefab-importer-smoke.log", status, StringComparison.Ordinal);
        Assert.Contains("[VanillaScopeReferenceImporter] COMPARE:", status, StringComparison.Ordinal);
        Assert.Contains("if ($null -eq $content)", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", action, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", action, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", status, StringComparison.Ordinal);
    }

    [Fact]
    public void Full_private_prefab_audit_is_query_bound_and_reports_serialized_field_coverage()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var actionStart = pipeline.IndexOf("function Invoke-UnityVanillaPrefabAudit", StringComparison.Ordinal);
        Assert.True(actionStart >= 0,
            "Pipeline must expose a generic private prefab serialized-field audit action.");
        var statusStart = pipeline.IndexOf("function Get-UnityVanillaPrefabImportStatus", actionStart, StringComparison.Ordinal);

        Assert.True(statusStart > actionStart,
            "Pipeline must expose a generic private prefab serialized-field audit action.");
        var action = pipeline[actionStart..statusStart];
        var statusEnd = pipeline.IndexOf("function Get-UnityVanillaScopeImportStatus", statusStart, StringComparison.Ordinal);
        Assert.True(statusEnd > statusStart,
            "Pipeline must report generic private prefab audit state through the bounded status reader.");
        var status = pipeline[statusStart..statusEnd];

        Assert.Contains("'UnityVanillaPrefabAuditNightForce'", pipeline, StringComparison.Ordinal);
        Assert.Contains("UnityVanillaPrefabAuditNightForce", wrapper, StringComparison.Ordinal);
        Assert.Contains("UnityVanillaPrefabAuditNightForce requires -Query <prefab name>.", action, StringComparison.Ordinal);
        Assert.Contains("HLin_Mods.PrivateTools.VanillaScopeReferenceImporter.RunRequestedPrefabAuditAgainstNightForce", action, StringComparison.Ordinal);
        Assert.Contains("Wait-ForVanillaPrefabImporterResult", action, StringComparison.Ordinal);
        Assert.Contains("[VanillaScopeReferenceImporter] AUDIT:", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", action, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", action, StringComparison.Ordinal);
    }

    [Fact]
    public void Unity_vanilla_scope_import_status_is_read_only_and_exposed_through_remote_wrapper()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var statusStart = pipeline.IndexOf("function Get-UnityVanillaScopeImportStatus", StringComparison.Ordinal);
        var statusEnd = pipeline.IndexOf("function Get-PrivateAssetArchiveStatus", statusStart, StringComparison.Ordinal);

        Assert.True(statusStart >= 0 && statusEnd > statusStart,
            "Pipeline must expose a read-only vanilla scope importer status action.");
        var status = pipeline[statusStart..statusEnd];

        Assert.Contains("'UnityVanillaImportStatus'", pipeline, StringComparison.Ordinal);
        Assert.Contains("UnityVanillaImportStatus", wrapper, StringComparison.Ordinal);
        Assert.Contains("vanilla-scope-importer-smoke.log", status, StringComparison.Ordinal);
        Assert.Contains("$batchWorkers", status, StringComparison.Ordinal);
        Assert.Contains("Unity batch worker:", status, StringComparison.Ordinal);
        Assert.Contains("Script rebind:", status, StringComparison.Ordinal);
        Assert.Contains("Write-Host (\"Script rebind:", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Write-Host \"Script rebind:", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy-Item", status, StringComparison.Ordinal);
    }

    [Fact]
    public void Unity_package_and_deploy_diagnostics_do_not_expose_private_paths()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var packageStart = pipeline.IndexOf("function New-UnityPackage", StringComparison.Ordinal);
        var packageEnd = pipeline.IndexOf("function New-Package", packageStart, StringComparison.Ordinal);
        var deployStart = pipeline.IndexOf("function Invoke-Deploy", StringComparison.Ordinal);
        var deployEnd = pipeline.IndexOf("function Invoke-WindowsShutdown", deployStart, StringComparison.Ordinal);

        Assert.True(packageStart >= 0 && packageEnd > packageStart,
            "Pipeline must expose private-path-safe Unity package diagnostics.");
        Assert.True(deployStart >= 0 && deployEnd > deployStart,
            "Pipeline must expose private-path-safe deploy diagnostics.");
        var package = pipeline[packageStart..packageEnd];
        var deploy = pipeline[deployStart..deployEnd];

        Assert.Contains("Unity package does not exist after the requested build step.", package,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Unity package does not exist: $sourcePackagePath", package,
            StringComparison.Ordinal);
        Assert.Contains("configured r2modman Default profile", deploy, StringComparison.Ordinal);
        Assert.Contains("Get-Process -Name 'h3vr'", deploy, StringComparison.Ordinal);
        Assert.Contains("H3VR is running. Close H3VR before deployment.", deploy, StringComparison.Ordinal);
        Assert.True(deploy.IndexOf("Get-Process -Name 'h3vr'", StringComparison.Ordinal) <
            deploy.IndexOf("New-Package $ModConfig", StringComparison.Ordinal),
            "Deploy must refuse an active H3VR process before it creates or replaces a package.");
        Assert.Contains("New-Package $ModConfig -ReuseExistingUnityPackage:$ReuseExistingUnityPackage", deploy,
            StringComparison.Ordinal);
        Assert.Contains("New-Package (Get-ModConfig $Mod) -ReuseExistingUnityPackage:$ReuseExistingUnityPackage", pipeline,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Deployed $Mod to $target", deploy, StringComparison.Ordinal);
        Assert.DoesNotContain("VR receipt: $vrReceipt", deploy, StringComparison.Ordinal);
    }

    [Fact]
    public void Package_command_reports_version_and_hash_without_artifact_path()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var packageStart = pipeline.IndexOf("'Package' {", StringComparison.Ordinal);
        var packageEnd = pipeline.IndexOf("'Deploy' {", packageStart, StringComparison.Ordinal);

        Assert.True(packageStart >= 0 && packageEnd > packageStart,
            "Pipeline must expose the package action dispatch.");
        var package = pipeline[packageStart..packageEnd];

        Assert.Contains("Packaged $Mod version", package, StringComparison.Ordinal);
        Assert.Contains("SHA-256", package, StringComparison.Ordinal);
        Assert.DoesNotContain("Format-List", package, StringComparison.Ordinal);
        Assert.DoesNotContain("ZipPath", package, StringComparison.Ordinal);
    }

    [Fact]
    public void Unsafe_private_scope_imports_can_be_moved_to_private_quarantine()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));
        var wrapper = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr-remote.sh"));
        var actionStart = pipeline.IndexOf("function Move-PrivateVanillaScopeImportsToQuarantine", StringComparison.Ordinal);
        var actionEnd = pipeline.IndexOf("function Invoke-Publish", actionStart, StringComparison.Ordinal);

        Assert.True(actionStart >= 0 && actionEnd > actionStart,
            "Pipeline must expose a recoverable private-import quarantine action.");
        var action = pipeline[actionStart..actionEnd];

        Assert.Contains("'QuarantineVanillaScopeImports'", pipeline, StringComparison.Ordinal);
        Assert.Contains("QuarantineVanillaScopeImports", wrapper, StringComparison.Ordinal);
        Assert.Contains("H3VR_PRIVATE_ASSET_LAB", action, StringComparison.Ordinal);
        Assert.Contains("Assets\\Projects\\PrivateVanillaPrefabReferences", action, StringComparison.Ordinal);
        Assert.Contains("$sourceDirectories", action, StringComparison.Ordinal);
        Assert.Contains("foreach ($sourceDirectory in $sourceDirectories)", action, StringComparison.Ordinal);
        Assert.Contains("Move-Item -LiteralPath $sourceDirectory", action, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", action, StringComparison.Ordinal);
        Assert.Contains("moved, not deleted", action, StringComparison.Ordinal);
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
