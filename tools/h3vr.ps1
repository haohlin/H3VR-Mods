[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Preflight', 'SourceStatus', 'RefreshSource', 'FindType', 'FindMethod', 'GrepSource', 'PrepareUnitySourceSync', 'SyncUnitySource', 'AuditItemId', 'AssetRipStatus', 'FindAssetRip', 'InspectAssetRip', 'UnityBuildStatus', 'Verify', 'Build', 'Test', 'Package', 'Deploy', 'Logs', 'TailLogs', 'ClearLogs', 'SetPublishToken', 'Publish')]
    [string]$Action,

    [ValidateSet('ThePing', 'GunGameProgressions', 'GunGameCursedRandom', 'BubbleLevel', 'NightForcePlus', 'NightForcePlusLegacy', 'Teleport', 'RemoveWhiteOut')]
    [string]$Mod = 'ThePing',

    [string]$EnvironmentConfigPath,
    [string]$ModsConfigPath,
    [string]$Query,
    [switch]$Publish,
    [switch]$VrApproved,
    [ValidateSet('Release', 'Debug')]
    [string]$GunGameBuildConfiguration = 'Release',
    [switch]$ReuseExistingUnityPackage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$BuildRoot = Join-Path $RepoRoot 'build'
$PublicEnvironmentConfigPath = Join-Path $BuildRoot 'environment.json'
$LocalEnvironmentConfigPath = Join-Path $BuildRoot 'environment.local.json'
$DefaultEnvironmentConfigPath = if (Test-Path -LiteralPath $LocalEnvironmentConfigPath) {
    $LocalEnvironmentConfigPath
}
else {
    $PublicEnvironmentConfigPath
}
$DefaultModsConfigPath = Join-Path $BuildRoot 'mods.json'

function Resolve-PipelineConfigPath {
    param(
        [string]$Path,
        [string]$DefaultPath,
        [string]$Label
    )

    $candidate = if ([string]::IsNullOrWhiteSpace($Path)) {
        $DefaultPath
    }
    elseif ([IO.Path]::IsPathRooted($Path)) {
        $Path
    }
    else {
        Join-Path $RepoRoot $Path
    }

    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "$Label does not exist: $candidate"
    }

    return (Resolve-Path -LiteralPath $candidate).Path
}

$EnvironmentConfigPath = Resolve-PipelineConfigPath -Path $EnvironmentConfigPath -DefaultPath $DefaultEnvironmentConfigPath -Label 'Environment configuration'
$ModsConfigPath = Resolve-PipelineConfigPath -Path $ModsConfigPath -DefaultPath $DefaultModsConfigPath -Label 'Mod configuration'
$EnvironmentConfig = Get-Content -LiteralPath $EnvironmentConfigPath -Raw | ConvertFrom-Json
$ModsConfig = Get-Content -LiteralPath $ModsConfigPath -Raw | ConvertFrom-Json

function Expand-EnvironmentConfiguration {
    param([object]$Config)

    foreach ($section in @($Config.h3vr, $Config.r2modman, $Config.unity)) {
        if ($null -eq $section) {
            continue
        }
        foreach ($property in $section.PSObject.Properties) {
            if ($property.Value -is [string]) {
                $property.Value = [Environment]::ExpandEnvironmentVariables($property.Value)
            }
        }
    }
}

Expand-EnvironmentConfiguration -Config $EnvironmentConfig

function Ensure-Directory {
    param([string]$Path)

    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Invoke-CheckedNative {
    param([scriptblock]$Command)

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Native command failed with exit code $LASTEXITCODE."
    }
}

function Get-ModConfig {
    param([string]$Name)

    $property = $ModsConfig.mods.PSObject.Properties[$Name]
    if ($null -eq $property) {
        throw "Unknown mod: $Name"
    }

    return $property.Value
}

function Get-FileSha256 {
    param([string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-CurrentCommit {
    Push-Location $RepoRoot
    try {
        return (git rev-parse HEAD).Trim()
    }
    finally {
        Pop-Location
    }
}

function Write-JsonFile {
    param(
        [string]$Path,
        [object]$Value
    )

    $Value | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-ProjectVersion {
    param([object]$ModConfig)

    if ($ModConfig.kind -eq 'dotnet') {
        [xml]$project = Get-Content -LiteralPath (Join-Path $RepoRoot $ModConfig.csproj) -Raw
        $version = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
        if ([string]::IsNullOrWhiteSpace($version)) {
            throw "No Version property found in $($ModConfig.csproj)."
        }

        return $version
    }

    if ($ModConfig.kind -eq 'unity') {
        $projectRoot = Get-UnityProjectRoot
        $profilePath = Join-Path $projectRoot $ModConfig.versionProfileRelativePath
        if (-not (Test-Path -LiteralPath $profilePath)) {
            throw "Unity build profile does not exist: $profilePath"
        }

        $profileContent = Get-Content -LiteralPath $profilePath -Raw
        $versionMatch = [regex]::Match($profileContent, '(?m)^\s*Version:\s*(?<version>\S+)\s*$')
        if (-not $versionMatch.Success) {
            throw "No Version field found in Unity build profile: $profilePath"
        }

        return $versionMatch.Groups['version'].Value
    }

    $manifestPath = Join-Path (Join-Path $RepoRoot $ModConfig.packageSource) 'manifest.json'
    return (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).version_number
}

function Get-UnityProjectRoot {
    $unityConfig = $EnvironmentConfig.PSObject.Properties['unity'].Value
    if ($null -eq $unityConfig -or [string]::IsNullOrWhiteSpace($unityConfig.meatKitProjectRoot)) {
        throw 'Unity project root is not configured. Set H3VR_UNITY_MEATKIT_PROJECT in private configuration.'
    }

    if (-not (Test-Path -LiteralPath $unityConfig.meatKitProjectRoot)) {
        throw "Unity project root does not exist: $($unityConfig.meatKitProjectRoot)"
    }

    return $unityConfig.meatKitProjectRoot
}

function Get-UnityPackageSourcePath {
    param(
        [object]$ModConfig,
        [string]$Version
    )

    $relativePath = $ModConfig.packageRelativePath.Replace('{version}', $Version)
    return Join-Path (Get-UnityProjectRoot) $relativePath
}

function Prepare-UnityProjectSourceSync {
    param([string]$Branch)

    if ([string]::IsNullOrWhiteSpace($Branch)) {
        throw 'PrepareUnitySourceSync requires -Query <branch>.'
    }

    $stagingRoot = Join-Path (Join-Path $BuildRoot 'staging') 'unity-source-sync'
    $payloadPath = Join-Path $stagingRoot 'NightForcePlus-source.zip'
    Ensure-Directory $stagingRoot
    [IO.File]::WriteAllBytes($payloadPath, [byte[]]@())
    Write-Host "Unity source upload prepared for branch $Branch. SHA256: $(Get-FileSha256 $payloadPath)"
}

function Sync-UnityProjectSource {
    param([string]$Branch)

    if ([string]::IsNullOrWhiteSpace($Branch)) {
        throw 'SyncUnitySource requires -Query <branch>.'
    }

    $projectRoot = Get-UnityProjectRoot
    $sourceRoot = Join-Path $projectRoot 'Assets\Projects'
    $openEditors = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine -like "*$projectRoot*" })
    if ($openEditors.Count -gt 0) {
        throw 'Windows Unity project is open. Close the editor before source sync.'
    }

    $stagingRoot = Join-Path (Join-Path $BuildRoot 'staging') 'unity-source-sync'
    $payloadPath = Join-Path $stagingRoot 'NightForcePlus-source.zip'
    $extractRoot = Join-Path $stagingRoot 'extracted'
    $sourceModRoot = Join-Path $extractRoot 'NightForcePlus'
    $targetModRoot = Join-Path $sourceRoot 'NightForcePlus'
    $sourceMeta = Join-Path $extractRoot 'NightForcePlus.meta'
    $targetMeta = Join-Path $sourceRoot 'NightForcePlus.meta'

    if (-not (Test-Path -LiteralPath $payloadPath -PathType Leaf)) {
        throw 'Unity source upload is missing. Run PrepareUnitySourceSync and upload the reviewed payload first.'
    }
    if (Test-Path -LiteralPath $extractRoot) {
        Remove-Item -LiteralPath $extractRoot -Recurse -Force
    }

    try {
        Expand-Archive -LiteralPath $payloadPath -DestinationPath $extractRoot -Force
        if (-not (Test-Path -LiteralPath $sourceModRoot -PathType Container) -or
            -not (Test-Path -LiteralPath $sourceMeta -PathType Leaf)) {
            throw 'Unity source branch lacks the NightForcePlus source payload.'
        }

        Ensure-Directory $sourceRoot
        Copy-Item -LiteralPath $sourceModRoot -Destination $sourceRoot -Recurse -Force
        Copy-Item -LiteralPath $sourceMeta -Destination $targetMeta -Force

        foreach ($sourceFile in @(Get-ChildItem -LiteralPath $sourceModRoot -Recurse -File)) {
            $relativePath = $sourceFile.FullName.Substring($sourceModRoot.Length).TrimStart('\')
            $targetFile = Join-Path $targetModRoot $relativePath
            if (-not (Test-Path -LiteralPath $targetFile -PathType Leaf) -or
                (Get-FileSha256 $sourceFile.FullName) -ne (Get-FileSha256 $targetFile)) {
                throw 'Windows Unity NightForcePlus source hash verification failed.'
            }
        }
        if ((Get-FileSha256 $sourceMeta) -ne (Get-FileSha256 $targetMeta)) {
            throw 'Windows Unity NightForcePlus folder metadata hash verification failed.'
        }
    }
    finally {
        if (Test-Path -LiteralPath $extractRoot) {
            Remove-Item -LiteralPath $extractRoot -Recurse -Force
        }
    }

    Write-Host "Unity NightForcePlus source payload synchronized for branch $Branch with verified hashes."
}

function Get-SourceManifestPath {
    return Join-Path $EnvironmentConfig.h3vr.dnspySourceRoot 'source-manifest.json'
}

function Get-SourceStatus {
    $errors = [System.Collections.Generic.List[string]]::new()
    $assemblies = @(
        @{ Name = 'Assembly-CSharp'; Path = $EnvironmentConfig.h3vr.assemblyCSharpDll },
        @{ Name = 'Assembly-CSharp-firstpass'; Path = $EnvironmentConfig.h3vr.assemblyCSharpFirstpassDll }
    )

    foreach ($assembly in $assemblies) {
        if (-not (Test-Path -LiteralPath $assembly.Path)) {
            $errors.Add("Missing live assembly: $($assembly.Path)")
        }
    }

    $manifestPath = Get-SourceManifestPath
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        $errors.Add("Missing generated source manifest: $manifestPath")
        return [PSCustomObject]@{ IsCurrent = $false; Errors = $errors }
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    foreach ($assembly in $assemblies) {
        if (-not (Test-Path -LiteralPath $assembly.Path)) {
            continue
        }

        $recordedHash = $manifest.assemblies.PSObject.Properties[$assembly.Name].Value.sha256
        $currentHash = Get-FileSha256 $assembly.Path
        if ($recordedHash -ne $currentHash) {
            $errors.Add("Generated source is stale for $($assembly.Name). Run RefreshSource.")
        }
    }

    return [PSCustomObject]@{ IsCurrent = $errors.Count -eq 0; Errors = $errors }
}

function Assert-CurrentSource {
    $status = Get-SourceStatus
    if (-not $status.IsCurrent) {
        throw ($status.Errors -join [Environment]::NewLine)
    }
}

function Invoke-RefreshSource {
    Ensure-Directory (Join-Path $BuildRoot 'staging')
    Push-Location $RepoRoot
    try {
        Invoke-CheckedNative { & dotnet tool restore }
    }
    finally {
        Pop-Location
    }

    $temporaryRoot = Join-Path (Join-Path $BuildRoot 'staging') ("decompile-" + [Guid]::NewGuid().ToString('N'))
    Ensure-Directory $temporaryRoot
    try {
        $assemblies = @(
            @{ Name = 'Assembly-CSharp'; Path = $EnvironmentConfig.h3vr.assemblyCSharpDll },
            @{ Name = 'Assembly-CSharp-firstpass'; Path = $EnvironmentConfig.h3vr.assemblyCSharpFirstpassDll }
        )

        foreach ($assembly in $assemblies) {
            if (-not (Test-Path -LiteralPath $assembly.Path)) {
                throw "Missing live assembly: $($assembly.Path)"
            }

            $outputPath = Join-Path $temporaryRoot $assembly.Name
            Push-Location $RepoRoot
            try {
                Invoke-CheckedNative { & dotnet tool run ilspycmd -- -p -o $outputPath $assembly.Path }
            }
            finally {
                Pop-Location
            }
        }

        Push-Location $RepoRoot
        try {
            $decompilerVersion = (& dotnet tool run ilspycmd -- --version | Out-String).Trim()
        }
        finally {
            Pop-Location
        }
        $manifest = [ordered]@{
            generatedAt = (Get-Date).ToUniversalTime().ToString('o')
            decompiler = $decompilerVersion
            assemblies = [ordered]@{
                'Assembly-CSharp' = [ordered]@{ path = $EnvironmentConfig.h3vr.assemblyCSharpDll; sha256 = Get-FileSha256 $EnvironmentConfig.h3vr.assemblyCSharpDll }
                'Assembly-CSharp-firstpass' = [ordered]@{ path = $EnvironmentConfig.h3vr.assemblyCSharpFirstpassDll; sha256 = Get-FileSha256 $EnvironmentConfig.h3vr.assemblyCSharpFirstpassDll }
            }
        }
        Write-JsonFile -Path (Join-Path $temporaryRoot 'source-manifest.json') -Value $manifest

        $sourceRoot = $EnvironmentConfig.h3vr.dnspySourceRoot
        if (Test-Path -LiteralPath $sourceRoot) {
            Remove-Item -LiteralPath $sourceRoot -Recurse -Force
        }
        Move-Item -LiteralPath $temporaryRoot -Destination $sourceRoot
        Write-Host "Refreshed generated source at $sourceRoot"
    }
    finally {
        if (Test-Path -LiteralPath $temporaryRoot) {
            Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
        }
    }
}

function Find-SourceText {
    param([string]$Pattern)

    Assert-CurrentSource
    Get-ChildItem -LiteralPath $EnvironmentConfig.h3vr.dnspySourceRoot -Filter '*.cs' -File -Recurse |
        Select-String -Pattern $Pattern
}

function Find-SourceType {
    param([string]$TypeName)

    return Find-SourceText "\b(class|struct|interface|enum)\s+$([regex]::Escape($TypeName))\b"
}

function Assert-PatchTargets {
    param([object]$ModConfig)

    foreach ($target in @($ModConfig.patchTargets)) {
        $typeMatches = @(Find-SourceType $target.type)
        if ($typeMatches.Count -eq 0) {
            throw "Patch target type was not found: $($target.type)"
        }

        $methodPattern = "\b$([regex]::Escape($target.method))\s*\("
        $methodMatches = @(Select-String -Path ($typeMatches.Path | Sort-Object -Unique) -Pattern $methodPattern)
        if ($methodMatches.Count -eq 0) {
            throw "Patch target method was not found: $($target.type).$($target.method)"
        }
    }
}

function Assert-ExternalPatchTargets {
    param([object]$ModConfig)

    $targetsProperty = $ModConfig.PSObject.Properties['externalPatchTargets']
    if ($null -eq $targetsProperty) {
        return
    }

    foreach ($target in @($targetsProperty.Value)) {
        $assemblyPath = Join-Path $EnvironmentConfig.r2modman.pluginsRoot $target.relativeAssemblyPath
        if (-not (Test-Path -LiteralPath $assemblyPath)) {
            throw "External patch assembly was not found: $($target.relativeAssemblyPath)"
        }

        Push-Location $RepoRoot
        try {
            $decompiledType = (& dotnet tool run ilspycmd -- -t $target.type $assemblyPath | Out-String)
            if ($LASTEXITCODE -ne 0) {
                throw "Could not inspect external patch type: $($target.type)"
            }
        }
        finally {
            Pop-Location
        }

        $methodPattern = "\b$([regex]::Escape($target.method))\s*\("
        if ($decompiledType -notmatch $methodPattern) {
            throw "External patch method was not found: $($target.type).$($target.method)"
        }
    }
}

function Invoke-Preflight {
    if (-not (Test-Path -LiteralPath $RepoRoot)) {
        throw "Repository root does not exist: $RepoRoot"
    }

    Write-Host "Repository: $RepoRoot"
    $branch = (& git -C $RepoRoot branch --show-current | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($branch)) {
        $branch = 'detached HEAD'
    }
    Write-Host "Git branch: $branch"
    git -C $RepoRoot status --short
    & dotnet --version

    foreach ($path in @($EnvironmentConfig.h3vr.assemblyCSharpDll, $EnvironmentConfig.h3vr.assemblyCSharpFirstpassDll, $EnvironmentConfig.r2modman.pluginsRoot, $EnvironmentConfig.r2modman.logPath)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Required environment path is missing: $path"
        }
    }

    $sourceStatus = Get-SourceStatus
    if ($sourceStatus.IsCurrent) {
        Write-Host 'Generated source is current.'
    }
    else {
        Write-Warning ($sourceStatus.Errors -join ' ')
    }
}

function Invoke-DotNetBuild {
    param([object]$ModConfig)

    Invoke-CheckedNative { & dotnet build (Join-Path $RepoRoot $ModConfig.csproj) -c Release }
}

function Wait-ForUnityProjectBatchWorker {
    param(
        [string]$ProjectRoot,
        [int]$StartupTimeoutSeconds = 60,
        [int]$CompletionTimeoutSeconds = 900
    )

    $startupDeadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
    $observedWorker = $false
    do {
        $workers = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
            Where-Object {
                $_.CommandLine -and $_.CommandLine -like '*-batchmode*' -and
                $_.CommandLine -like "*$ProjectRoot*"
            })
        if ($workers.Count -gt 0) {
            $observedWorker = $true
            break
        }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $startupDeadline)

    if (-not $observedWorker) {
        return $false
    }

    $completionDeadline = (Get-Date).AddSeconds($CompletionTimeoutSeconds)
    do {
        $workers = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
            Where-Object {
                $_.CommandLine -and $_.CommandLine -like '*-batchmode*' -and
                $_.CommandLine -like "*$ProjectRoot*"
            })
        if ($workers.Count -eq 0) {
            return $true
        }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $completionDeadline)

    throw "Unity batch worker did not exit before timeout for project: $ProjectRoot"
}

function Wait-ForUnityBuildOutput {
    param(
        [string]$LogPath,
        [string]$SuccessMarker,
        [string]$PackagePath,
        [int]$TimeoutSeconds = 900
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $hasMarker = (Test-Path -LiteralPath $LogPath) -and
            (Select-String -LiteralPath $LogPath -Pattern $SuccessMarker -SimpleMatch -Quiet -ErrorAction SilentlyContinue)
        if ($hasMarker -and (Test-Path -LiteralPath $PackagePath)) {
            return $true
        }

        if ((Test-Path -LiteralPath $LogPath) -and
            (Select-String -LiteralPath $LogPath -Pattern 'executeMethod method .* threw exception|Aborting batchmode due to failure' -Quiet -ErrorAction SilentlyContinue)) {
            return $false
        }

        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)

    return $false
}

function Invoke-UnityBuild {
    param([object]$ModConfig)

    $projectRoot = Get-UnityProjectRoot
    $unityConfig = $EnvironmentConfig.PSObject.Properties['unity'].Value
    if ([string]::IsNullOrWhiteSpace($unityConfig.editorExecutable) -or
        -not (Test-Path -LiteralPath $unityConfig.editorExecutable)) {
        throw 'Unity editor is not configured. Set H3VR_UNITY_EXECUTABLE in private configuration.'
    }

    $version = Get-ProjectVersion $ModConfig
    $packagePath = Get-UnityPackageSourcePath -ModConfig $ModConfig -Version $version
    if (Test-Path -LiteralPath $packagePath) {
        Remove-Item -LiteralPath $packagePath -Force
    }

    if ([string]::IsNullOrWhiteSpace($ModConfig.unityBuildSuccessMarker)) {
        throw "Unity mod $Mod is missing unityBuildSuccessMarker."
    }

    $logDirectory = Join-Path (Join-Path $BuildRoot 'staging') 'unity-logs'
    Ensure-Directory $logDirectory
    $logPath = Join-Path $logDirectory ("$Mod-$version-build.log")
    $buildCompleted = $false
    for ($attempt = 1; $attempt -le 2; $attempt++) {
        if (Test-Path -LiteralPath $logPath) {
            Remove-Item -LiteralPath $logPath -Force -ErrorAction Stop
        }
        Write-Host "Building $Mod with Unity batch mode (attempt $attempt/2)."
        $arguments = @(
            '-batchmode',
            '-nographics',
            '-quit',
            '-projectPath', ('"{0}"' -f $projectRoot),
            '-executeMethod', $ModConfig.unityBuildMethod,
            '-logFile', ('"{0}"' -f $logPath)
        )
        $process = Start-Process -FilePath $unityConfig.editorExecutable -ArgumentList $arguments -Wait -PassThru
        $buildCompleted = Wait-ForUnityBuildOutput -LogPath $logPath -SuccessMarker $ModConfig.unityBuildSuccessMarker -PackagePath $packagePath
        if ($process.ExitCode -ne 0 -and -not $buildCompleted) {
            throw "Unity batch build failed with exit code $($process.ExitCode). See $logPath"
        }
        if ($process.ExitCode -ne 0) {
            Write-Warning "Unity launcher exited with code $($process.ExitCode), but detached batch worker completed."
        }

        if ($buildCompleted) {
            break
        }

        if ($attempt -eq 1) {
            Write-Warning "Unity recompiled scripts before executing $($ModConfig.unityBuildMethod); retrying once."
        }
    }

    if (-not $buildCompleted -or -not (Test-Path -LiteralPath $packagePath)) {
        throw "Unity did not complete $($ModConfig.unityBuildMethod) with expected package: $packagePath"
    }

    return $packagePath
}

function Get-UnityBuildStatus {
    param([object]$ModConfig)

    if ($ModConfig.kind -ne 'unity') {
        throw "UnityBuildStatus requires a Unity mod."
    }

    $version = Get-ProjectVersion $ModConfig
    $packagePath = Get-UnityPackageSourcePath -ModConfig $ModConfig -Version $version
    $logPath = Join-Path (Join-Path (Join-Path $BuildRoot 'staging') 'unity-logs') ("$Mod-$version-build.log")
    $hasSourcePackage = Test-Path -LiteralPath $packagePath
    $hasSuccessMarker = (Test-Path -LiteralPath $logPath) -and
        (Select-String -LiteralPath $logPath -Pattern $ModConfig.unityBuildSuccessMarker -SimpleMatch -Quiet -ErrorAction SilentlyContinue)
    $hasFailureMarker = (Test-Path -LiteralPath $logPath) -and
        (Select-String -LiteralPath $logPath -Pattern 'executeMethod method .* threw exception|Aborting batchmode due to failure' -Quiet -ErrorAction SilentlyContinue)
    $runtimeDiagnosticLines = if (Test-Path -LiteralPath $logPath) {
        @(Select-String -LiteralPath $logPath -Pattern '\[NightForcePlusRuntime\]' -ErrorAction SilentlyContinue)
    }
    else {
        @()
    }
    $runtimeDiagnostics = $runtimeDiagnosticLines.Count
    $lastRuntimeDiagnostic = if ($runtimeDiagnostics -gt 0) {
        ($runtimeDiagnosticLines[-1].Line -replace '[A-Za-z]:\\[^\s]+', '<private-path>').Trim()
    }
    else {
        'none'
    }
    $state = if ($hasSourcePackage -and $hasSuccessMarker) { 'ready' } elseif ($hasSourcePackage) { 'package-without-success-marker' } elseif ($hasSuccessMarker) { 'marker-without-package' } else { 'pending-or-failed' }

    Write-Host "Unity build status: $state"
    Write-Host "Success marker present: $hasSuccessMarker"
    Write-Host "Build failure marker present: $hasFailureMarker"
    Write-Host "NightForce runtime diagnostics: $runtimeDiagnostics"
    Write-Host "Last NightForce runtime diagnostic: $lastRuntimeDiagnostic"
    Write-Host "Source package present: $hasSourcePackage"
    if ($hasSourcePackage) {
        Write-Host "Source package SHA-256: $(Get-FileSha256 $packagePath)"
    }
}

function Get-GunGameStagingPath {
    return Join-Path (Join-Path $BuildRoot 'staging') 'GunGameProgressions-generator'
}

function Test-GunGamePools {
    param([string]$Path)

    $offlineMetadataPath = Join-Path $Path 'ObjectData.json'
    if (-not (Test-Path -LiteralPath $offlineMetadataPath)) {
        throw "Missing versioned GunGame vanilla metadata snapshot: $offlineMetadataPath"
    }

    $offlineMetadataRoot = Get-Content -LiteralPath $offlineMetadataPath -Raw | ConvertFrom-Json
    $offlineMetadata = [object[]]$offlineMetadataRoot
    if ($offlineMetadata.Count -eq 0 -or @($offlineMetadata | Where-Object { $_.IsModContent -eq $true }).Count -ne 0) {
        throw "GunGame offline metadata must contain only vanilla entries."
    }
    $offlineMetadataById = @{}
    foreach ($entry in $offlineMetadata) {
        if (-not [string]::IsNullOrWhiteSpace($entry.ObjectID)) {
            $offlineMetadataById[$entry.ObjectID] = $entry
        }
    }

    $offlinePoolNames = @(
        'GunGameWeaponPool_Runtime_01_Vanilla_Rot_RW_Rot.json',
        'GunGameWeaponPool_Runtime_03_Vanilla_Mixed_Enemy_RW_Rot.json'
    )
    $pools = @(Get-ChildItem -LiteralPath $Path -File -Filter 'GunGameWeaponPool_Runtime_*.json' | Where-Object { $_.Name -notmatch 'OLD' } | Sort-Object Name)
    if ((($pools.Name | Sort-Object) -join '|') -ne (($offlinePoolNames | Sort-Object) -join '|')) {
        throw "Expected $($offlinePoolNames -join ', '), found $($pools.Name -join ', ')."
    }

    foreach ($pool in $pools) {
        $data = Get-Content -LiteralPath $pool.FullName -Raw | ConvertFrom-Json
        if ($data.WeaponPoolType -ne 'Advanced' -or @($data.Guns).Count -eq 0 -or
            @($data.Enemies).Count -eq 0) {
            throw "Invalid GunGame advanced pool: $($pool.Name)"
        }

        if ($pool.Name -eq 'GunGameWeaponPool_Runtime_01_Vanilla_Rot_RW_Rot.json' -and
            (@($data.Enemies).Count -ne 1 -or $data.Enemies[0].EnemyNameString -ne 'RW_Rot' -or $data.Name -ne 'Runtime 01 - Vanilla Rot')) {
            throw "Invalid GunGame vanilla Rot fallback: $($pool.Name)"
        }

        if ($pool.Name -eq 'GunGameWeaponPool_Runtime_03_Vanilla_Mixed_Enemy_RW_Rot.json' -and
            (@($data.Enemies).Count -le 1 -or $data.EnemyProgressionType -ne 0 -or $data.Name -ne 'Runtime 03 - Vanilla Mixed Enemy')) {
            throw "Invalid GunGame vanilla mixed-enemy fallback: $($pool.Name)"
        }

        foreach ($gun in @($data.Guns)) {
            $hasValidFeed =
                -not [string]::IsNullOrWhiteSpace($gun.MagName) -and
                @($gun.MagNames).Count -gt 0 -and
                $gun.CategoryID -in @(0, 1, 2) -and
                $gun.MagName -in @($gun.MagNames)
            $sourceFirearm = $offlineMetadataById[$gun.GunName]
            $hasApprovedEmptyFeed =
                [string]::IsNullOrWhiteSpace($gun.MagName) -and
                @($gun.MagNames).Count -eq 0 -and
                $gun.CategoryID -eq 0 -and
                $sourceFirearm -ne $null -and
                $sourceFirearm.Category -eq 'Firearm' -and
                $sourceFirearm.IsGunGameRoundDisplaySupported -and
                $gun.GunName -eq 'GravitonBeamer'
            if ([string]::IsNullOrWhiteSpace($gun.GunName) -or
                (-not $hasValidFeed -and -not $hasApprovedEmptyFeed)) {
                throw "Invalid GunGame advanced weapon: $($pool.Name) / $($gun.GunName)"
            }
        }
    }
}

function Invoke-GunGameBuild {
    param(
        [object]$ModConfig,
        [ValidateSet('Release', 'Debug')]
        [string]$BuildConfiguration = 'Release'
    )

    $stagingPath = Get-GunGameStagingPath
    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
    Ensure-Directory $stagingPath

    Invoke-CheckedNative { & dotnet build (Join-Path $RepoRoot $ModConfig.metadataExporterCsproj) -c $BuildConfiguration }

    $sourcePath = Join-Path $RepoRoot 'GunGameProgressions'
    $profileSourcePath = Join-Path $RepoRoot $ModConfig.profileSource
    $offlineMetadataPath = Join-Path $profileSourcePath 'ObjectData.json'
    $offlineGeneratorProject = Join-Path $RepoRoot $ModConfig.offlineProfileGeneratorCsproj
    Invoke-CheckedNative {
        & dotnet run --project $offlineGeneratorProject -c Release -- --input $offlineMetadataPath --output-dir $profileSourcePath --verify
    }

    $offlinePoolNames = @(
        'GunGameWeaponPool_Runtime_01_Vanilla_Rot_RW_Rot.json',
        'GunGameWeaponPool_Runtime_03_Vanilla_Mixed_Enemy_RW_Rot.json'
    )
    $offlinePoolPaths = @($offlinePoolNames | ForEach-Object { Join-Path $profileSourcePath $_ })
    foreach ($offlinePoolPath in $offlinePoolPaths) {
        if (-not (Test-Path -LiteralPath $offlinePoolPath)) {
            throw "Missing offline GunGame fallback profile: $offlinePoolPath"
        }
    }
    Write-Host "Using tracked GunGame fallback profiles verified against the shared runtime resolver."
    Copy-Item -LiteralPath (Join-Path $sourcePath 'profile-rules.json') -Destination $stagingPath
    Copy-Item -LiteralPath $offlineMetadataPath -Destination $stagingPath
    $offlinePoolPaths | ForEach-Object { Copy-Item -LiteralPath $_ -Destination $stagingPath }
    Test-GunGamePools $stagingPath
    return $stagingPath
}

function Invoke-Build {
    param(
        [object]$ModConfig,
        [ValidateSet('Release', 'Debug')]
        [string]$BuildConfiguration = 'Release'
    )

    if ($ModConfig.kind -eq 'dotnet') {
        Invoke-DotNetBuild $ModConfig
        return
    }

    if ($ModConfig.kind -eq 'python') {
        Invoke-GunGameBuild -ModConfig $ModConfig -BuildConfiguration $BuildConfiguration | Out-Null
        return
    }

    if ($ModConfig.kind -eq 'unity') {
        Invoke-UnityBuild $ModConfig | Out-Null
        return
    }

    throw "Unsupported build kind: $($ModConfig.kind)"
}

function Invoke-Test {
    $previousRuntimeTestFlag = $env:H3VR_METADATA_EXPORTER_TESTS
    try {
        $env:H3VR_METADATA_EXPORTER_TESTS = '1'
        Invoke-CheckedNative { & dotnet test (Join-Path $RepoRoot 'tests\H3vrPipeline.Tests\H3vrPipeline.Tests.csproj') }
    }
    finally {
        if ($null -eq $previousRuntimeTestFlag) {
            Remove-Item Env:H3VR_METADATA_EXPORTER_TESTS -ErrorAction SilentlyContinue
        }
        else {
            $env:H3VR_METADATA_EXPORTER_TESTS = $previousRuntimeTestFlag
        }
    }
}

function Copy-PackageMetadata {
    param(
        [object]$ModConfig,
        [string]$PackageRoot,
        [string]$Version
    )

    $source = Join-Path $RepoRoot $ModConfig.packageSource
    foreach ($name in @('README.md', 'CHANGELOG.md', 'icon.png')) {
        Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $PackageRoot $name)
    }

    $manifest = Get-Content -LiteralPath (Join-Path $source 'manifest.json') -Raw | ConvertFrom-Json
    $manifest.version_number = $Version
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $PackageRoot 'manifest.json') -Encoding UTF8
}

function Copy-Payload {
    param(
        [object]$ModConfig,
        [string]$PackageRoot,
        [ValidateSet('Release', 'Debug')]
        [string]$BuildConfiguration = 'Release'
    )

    foreach ($payload in @($ModConfig.payload)) {
        $payloadSource = ([string]$payload.from).Replace('{configuration}', $BuildConfiguration)
        if ($payloadSource -eq 'generated') {
            $source = Get-GunGameStagingPath
        }
        else {
            $source = Join-Path $RepoRoot $payloadSource
        }

        if (-not (Test-Path -LiteralPath $source)) {
            throw "Package payload does not exist: $source"
        }

        if ((Get-Item -LiteralPath $source).PSIsContainer) {
            $files = if ($payloadSource -eq 'generated') {
                @(Get-ChildItem -LiteralPath $source -File -Filter 'GunGameWeaponPool_Runtime_*.json') +
                @(Get-Item -LiteralPath (Join-Path $source 'profile-rules.json'))
            }
            else {
                @(Get-ChildItem -LiteralPath $source -File)
            }
            $files | ForEach-Object {
                Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $PackageRoot $_.Name)
            }
        }
        else {
            $destination = Join-Path $PackageRoot $payload.to
            Ensure-Directory (Split-Path -Parent $destination)
            Copy-Item -LiteralPath $source -Destination $destination
        }
    }
}

function Get-ModProjectDirectory {
    param([object]$ModConfig)

    $property = $ModConfig.PSObject.Properties['projectDir']
    if ($null -eq $property) {
        return $null
    }

    return [string]$property.Value
}

function Get-EffectiveBuildConfiguration {
    param([object]$ModConfig)

    if ((Get-ModProjectDirectory $ModConfig) -eq 'GunGameProgressions') {
        return $GunGameBuildConfiguration
    }

    return 'Release'
}

function Assert-GunGameReleasePackage {
    param(
        [object]$ModConfig,
        [string]$PackagePath,
        [string]$BuildConfiguration
    )

    if ((Get-ModProjectDirectory $ModConfig) -ne 'GunGameProgressions' -or $BuildConfiguration -ne 'Release') {
        return
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        if (@($archive.Entries | Where-Object { $_.FullName -like '*_05_Compatibility_Probe_*' }).Count -ne 0) {
            throw 'GunGame release package must not contain Runtime 05 pool files.'
        }
    }
    finally {
        $archive.Dispose()
    }
}

function New-UnityPackage {
    param([object]$ModConfig)

    $version = Get-ProjectVersion $ModConfig
    if ($ReuseExistingUnityPackage) {
        Write-Host "Reusing existing Unity package for $Mod $version by explicit request."
    }
    else {
        Invoke-UnityBuild $ModConfig | Out-Null
    }

    $sourcePackagePath = Get-UnityPackageSourcePath -ModConfig $ModConfig -Version $version
    if (-not (Test-Path -LiteralPath $sourcePackagePath)) {
        throw "Unity package does not exist: $sourcePackagePath"
    }

    $artifactDirectory = Join-Path (Join-Path (Join-Path $BuildRoot 'artifacts') $Mod) $version
    Ensure-Directory $artifactDirectory
    $zipPath = Join-Path $artifactDirectory ("$($ModConfig.namespace)-$($ModConfig.packageName)-$version.zip")
    Copy-Item -LiteralPath $sourcePackagePath -Destination $zipPath -Force

    Push-Location $RepoRoot
    try {
        Invoke-CheckedNative { & dotnet run --project (Join-Path $RepoRoot 'tools\H3vrPipeline\H3vrPipeline.csproj') -- validate $zipPath $ModConfig.layout }
    }
    finally {
        Pop-Location
    }

    $receiptDirectory = Join-Path $BuildRoot 'receipts'
    Ensure-Directory $receiptDirectory
    $receiptPath = Join-Path $receiptDirectory ("$Mod-$version-package.json")
    Write-JsonFile -Path $receiptPath -Value ([ordered]@{
        mod = $Mod
        version = $version
        commit = Get-CurrentCommit
        packagePath = $zipPath
        sha256 = Get-FileSha256 $zipPath
        sourcePackageSha256 = Get-FileSha256 $sourcePackagePath
        reusedExistingUnityPackage = [bool]$ReuseExistingUnityPackage
        createdAt = (Get-Date).ToUniversalTime().ToString('o')
    })

    return [PSCustomObject]@{ Version = $version; ZipPath = $zipPath; Sha256 = Get-FileSha256 $zipPath }
}

function New-Package {
    param([object]$ModConfig)

    if ($ModConfig.kind -eq 'unity') {
        return New-UnityPackage $ModConfig
    }

    $buildConfiguration = Get-EffectiveBuildConfiguration $ModConfig
    Invoke-Build -ModConfig $ModConfig -BuildConfiguration $buildConfiguration | Out-Null
    $version = Get-ProjectVersion $ModConfig
    $artifactVersion = if ($buildConfiguration -eq 'Debug') { $version + '-debug' } else { $version }
    $artifactDirectory = Join-Path (Join-Path (Join-Path $BuildRoot 'artifacts') $Mod) $artifactVersion
    $stagingDirectory = Join-Path (Join-Path $BuildRoot 'staging') ("package-" + $Mod)
    $packageRoot = Join-Path $stagingDirectory 'payload'
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Ensure-Directory $packageRoot

    Copy-PackageMetadata -ModConfig $ModConfig -PackageRoot $packageRoot -Version $version
    Copy-Payload -ModConfig $ModConfig -PackageRoot $packageRoot -BuildConfiguration $buildConfiguration

    Ensure-Directory $artifactDirectory
    $zipPath = Join-Path $artifactDirectory ("$($ModConfig.namespace)-$($ModConfig.packageName)-$artifactVersion.zip")
    Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $zipPath)
    Assert-GunGameReleasePackage -ModConfig $ModConfig -PackagePath $zipPath -BuildConfiguration $buildConfiguration

    Push-Location $RepoRoot
    try {
        Invoke-CheckedNative { & dotnet run --project (Join-Path $RepoRoot 'tools\H3vrPipeline\H3vrPipeline.csproj') -- validate $zipPath $ModConfig.layout }
    }
    finally {
        Pop-Location
    }

    $receiptDirectory = Join-Path $BuildRoot 'receipts'
    Ensure-Directory $receiptDirectory
    $receiptPath = Join-Path $receiptDirectory ("$Mod-$version-package.json")
    Write-JsonFile -Path $receiptPath -Value ([ordered]@{
        mod = $Mod
        version = $version
        commit = Get-CurrentCommit
        buildConfiguration = $buildConfiguration
        packagePath = $zipPath
        sha256 = Get-FileSha256 $zipPath
        createdAt = (Get-Date).ToUniversalTime().ToString('o')
    })

    return [PSCustomObject]@{ Version = $version; BuildConfiguration = $buildConfiguration; ZipPath = $zipPath; Sha256 = Get-FileSha256 $zipPath }
}

function New-VrReceipt {
    param(
        [object]$Package,
        [string]$DeployPath
    )

    $receiptDirectory = Join-Path $BuildRoot 'receipts'
    Ensure-Directory $receiptDirectory
    $timestamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
    $receiptPath = Join-Path $receiptDirectory ("$Mod-$($Package.Version)-$timestamp-vrtest.md")
    @"
# $Mod $($Package.Version) VR Test

Package SHA256: $($Package.Sha256)
Git commit: $(Get-CurrentCommit)
Deploy path: $DeployPath
BepInEx log path: $($EnvironmentConfig.r2modman.logPath)
Result: PENDING

## Checklist
- [ ] r2modman Default profile launches
- [ ] BepInEx logs plugin load
- [ ] Expected mod behavior works in VR
- [ ] No Harmony or dependency exceptions in the BepInEx log

## Notes

"@ | Set-Content -LiteralPath $receiptPath -Encoding UTF8

    return $receiptPath
}

function Invoke-Deploy {
    param([object]$ModConfig)

    $package = New-Package $ModConfig
    $deployStaging = Join-Path (Join-Path $BuildRoot 'staging') ("deploy-" + $Mod)
    Remove-Item -LiteralPath $deployStaging -Recurse -Force -ErrorAction SilentlyContinue
    Ensure-Directory $deployStaging
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($package.ZipPath, $deployStaging)

    $target = Join-Path $EnvironmentConfig.r2modman.pluginsRoot $ModConfig.deploymentFolder
    $backupRoot = Join-Path (Join-Path $BuildRoot 'receipts') 'backups'
    Ensure-Directory $backupRoot
    if (Test-Path -LiteralPath $target) {
        Copy-Item -LiteralPath $target -Destination (Join-Path $backupRoot ("$($ModConfig.deploymentFolder)-" + (Get-Date).ToString('yyyyMMdd-HHmmss'))) -Recurse
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    Ensure-Directory $target

    if ($ModConfig.layout -eq 'legacy-flat') {
        Copy-Item -Path (Join-Path $deployStaging '*') -Destination $target -Recurse -Force
    }
    else {
        $payloadRoot = Join-Path $deployStaging ("BepInEx\\plugins\\" + $ModConfig.deploymentFolder)
        Copy-Item -Path (Join-Path $payloadRoot '*') -Destination $target -Recurse -Force
    }

    $vrReceipt = New-VrReceipt -Package $package -DeployPath $target
    Write-Host "Deployed $Mod to $target"
    Write-Host "VR receipt: $vrReceipt"
}

function Invoke-LogAction {
    param([string]$Mode)

    $logPath = $EnvironmentConfig.r2modman.logPath
    if ($Mode -eq 'clear') {
        $archiveDirectory = Join-Path (Join-Path $BuildRoot 'receipts') 'logs'
        Ensure-Directory $archiveDirectory
        Copy-Item -LiteralPath $logPath -Destination (Join-Path $archiveDirectory ("LogOutput-" + (Get-Date).ToString('yyyyMMdd-HHmmss') + '.log'))
        Clear-Content -LiteralPath $logPath
        return
    }

    $lines = if ($Mode -eq 'tail') { Get-Content -LiteralPath $logPath -Tail 300 } else { Get-Content -LiteralPath $logPath }
    $lines | Select-String -Pattern 'HLin|ThePing|GunGame|Harmony|Exception|Error|BepInEx'
}

function Test-FileContainsUtf8Text {
    param(
        [string]$Path,
        [string]$Needle
    )

    try {
        $contents = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
    }
    catch {
        return $false
    }
    return -not [string]::IsNullOrEmpty($Needle) -and
        $contents.IndexOf($Needle, [StringComparison]::Ordinal) -ge 0
}

function Find-InstalledItemId {
    param([string]$ItemId)

    if ([string]::IsNullOrWhiteSpace($ItemId)) {
        throw 'AuditItemId requires -Query <ItemID>.'
    }

    $packageDirectories = @(Get-ChildItem -LiteralPath $EnvironmentConfig.r2modman.pluginsRoot -Directory |
        Where-Object { $_.Name -like 'HLin*' -or $_.Name -like '*NightForce*' })
    Write-Host "Auditing $($packageDirectories.Count) candidate package(s) for ItemID text '$ItemId'."
    $auditMatches = @()

    foreach ($directory in $packageDirectories) {
        $matchedFiles = @()
        foreach ($file in @(Get-ChildItem -LiteralPath $directory.FullName -Recurse -File)) {
            if (Test-FileContainsUtf8Text -Path $file.FullName -Needle $ItemId) {
                $matchedFiles += $file.Name
            }
        }

        if ($matchedFiles.Count -gt 0) {
            $auditMatches += [PSCustomObject]@{
                Package = $directory.Name
                Files = ($matchedFiles | Sort-Object -Unique) -join ', '
            }
        }
    }

    Write-Host "ItemID audit found $($auditMatches.Count) matching package(s)."
    if ($auditMatches.Count -eq 0) {
        Write-Host "No HLin or NightForce package contains ItemID text '$ItemId'."
        return
    }

    foreach ($match in ($auditMatches | Sort-Object Package)) {
        Write-Host "ItemID match: $($match.Package) [$($match.Files)]"
    }
}

function Get-PrivateAssetArchiveStatus {
    $assetLab = [Environment]::GetEnvironmentVariable('H3VR_PRIVATE_ASSET_LAB')
    if ([string]::IsNullOrWhiteSpace($assetLab)) {
        throw 'H3VR_PRIVATE_ASSET_LAB is not configured on Windows.'
    }
    if (-not (Test-Path -LiteralPath $assetLab -PathType Container)) {
        throw 'H3VR_PRIVATE_ASSET_LAB does not point to an existing private asset lab.'
    }

    $manifestDirectory = Join-Path $assetLab 'manifests'
    $manifest = @(Get-ChildItem -LiteralPath $manifestDirectory -Filter 'H3VRFull-export-files-*.sha256.tsv' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1)[0]
    if ($null -eq $manifest) {
        throw 'Private asset lab has no H3VRFull SHA-256 manifest.'
    }

    $entries = @(Get-Content -LiteralPath $manifest.FullName)
    $categories = @{
        Mesh = @('Mesh', 'Meshes')
        Material = @('Material', 'Materials')
        Texture2D = @('Texture2D', 'Textures')
        Shader = @('Shader', 'Shaders')
        Prefab = @('Prefab', 'Prefabs')
    }
    $counts = [ordered]@{}
    foreach ($category in $categories.GetEnumerator()) {
        $counts[$category.Key] = @($entries | Where-Object {
            foreach ($segment in $category.Value) {
                if ($_ -match "(^|[\\/])$([regex]::Escape($segment))([\\/]|$)") {
                    return $true
                }
            }
            return $false
        }).Count
    }

    Write-Host 'Asset archive status: indexed'
    Write-Host "Manifest entries: $($entries.Count)"
    Write-Host "Mesh entries: $($counts.Mesh)"
    Write-Host "Material entries: $($counts.Material)"
    Write-Host "Texture2D entries: $($counts.Texture2D)"
    Write-Host "Shader entries: $($counts.Shader)"
    Write-Host "Prefab entries: $($counts.Prefab)"
    Write-Host "Materialized export roots: $(@(Get-PrivateAssetRipExportRoots -AssetLab $assetLab).Count)"
}

function Find-PrivateAssetRip {
    param([string]$SearchQuery)

    if ([string]::IsNullOrWhiteSpace($SearchQuery)) {
        throw 'FindAssetRip requires -Query <text>.'
    }

    $assetLab = [Environment]::GetEnvironmentVariable('H3VR_PRIVATE_ASSET_LAB')
    if ([string]::IsNullOrWhiteSpace($assetLab)) {
        throw 'H3VR_PRIVATE_ASSET_LAB is not configured on Windows.'
    }
    if (-not (Test-Path -LiteralPath $assetLab -PathType Container)) {
        throw 'H3VR_PRIVATE_ASSET_LAB does not point to an existing private asset lab.'
    }

    $manifestDirectory = Join-Path $assetLab 'manifests'
    $manifest = @(Get-ChildItem -LiteralPath $manifestDirectory -Filter 'H3VRFull-export-files-*.sha256.tsv' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1)[0]
    if ($null -eq $manifest) {
        throw 'Private asset lab has no H3VRFull SHA-256 manifest.'
    }

    $escapedQuery = [regex]::Escape($SearchQuery)
    $matches = @(Get-Content -LiteralPath $manifest.FullName | Where-Object { $_ -match $escapedQuery } | Select-Object -First 100)
    Write-Host "Asset archive search: $($matches.Count) match(es), showing at most 100."
    foreach ($match in $matches) {
        $columns = $match -split "`t", 2
        $sourcePath = $columns[0]
        $assetsOffset = $sourcePath.IndexOf('\Assets\', [System.StringComparison]::OrdinalIgnoreCase)
        if ($assetsOffset -lt 0) {
            $assetsOffset = $sourcePath.IndexOf('/Assets/', [System.StringComparison]::OrdinalIgnoreCase)
        }
        $safePath = if ($assetsOffset -ge 0) { $sourcePath.Substring($assetsOffset + 1) } else { Split-Path -Leaf $sourcePath }
        $metadata = if ($columns.Count -gt 1) { "`t$($columns[1])" } else { '' }
        Write-Host "Asset match: $safePath$metadata"
    }
}

function Get-PrivateAssetRipExportRoots {
    param([string]$AssetLab)

    $roots = [System.Collections.Generic.List[string]]::new()
    $pending = [System.Collections.Generic.Queue[object]]::new()
    $pending.Enqueue([pscustomobject]@{ Path = $AssetLab; Depth = 0 })
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    while ($pending.Count -gt 0) {
        $current = $pending.Dequeue()
        if (-not $seen.Add($current.Path)) {
            continue
        }
        if (Test-Path -LiteralPath (Join-Path $current.Path 'Assets') -PathType Container) {
            $roots.Add($current.Path)
        }
        if ($current.Depth -ge 3) {
            continue
        }
        foreach ($directory in Get-ChildItem -LiteralPath $current.Path -Directory -ErrorAction SilentlyContinue) {
            $pending.Enqueue([pscustomobject]@{ Path = $directory.FullName; Depth = ($current.Depth + 1) })
        }
    }
    return $roots
}

function Resolve-PrivateAssetRipSourceFile {
    param(
        [string]$AssetLab,
        [string]$ManifestSourcePath
    )

    $assetsOffset = $ManifestSourcePath.IndexOf('\Assets\', [System.StringComparison]::OrdinalIgnoreCase)
    if ($assetsOffset -lt 0) {
        $assetsOffset = $ManifestSourcePath.IndexOf('/Assets/', [System.StringComparison]::OrdinalIgnoreCase)
    }
    if ($assetsOffset -lt 0) {
        throw 'Archived manifest entry does not resolve below an Assets directory.'
    }

    $relativeAssetPath = $ManifestSourcePath.Substring($assetsOffset + 1)
    $candidateRoots = @(Get-PrivateAssetRipExportRoots -AssetLab $AssetLab)
    foreach ($candidateRoot in $candidateRoots) {
        $candidate = Join-Path $candidateRoot $relativeAssetPath
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [pscustomobject]@{
                SourcePath = $candidate
                AssetsRoot = Join-Path $candidateRoot 'Assets'
            }
        }
    }

    throw 'Archived manifest entry is indexed, but no materialized exported-project root contains its source file.'
}

function Get-PrivateAssetRipGraph {
    param([string]$PrefabName)

    if ([string]::IsNullOrWhiteSpace($PrefabName)) {
        throw 'InspectAssetRip requires -Query <prefab name>.'
    }

    $assetLab = [Environment]::GetEnvironmentVariable('H3VR_PRIVATE_ASSET_LAB')
    if ([string]::IsNullOrWhiteSpace($assetLab)) {
        throw 'H3VR_PRIVATE_ASSET_LAB is not configured on Windows.'
    }
    if (-not (Test-Path -LiteralPath $assetLab -PathType Container)) {
        throw 'H3VR_PRIVATE_ASSET_LAB does not point to an existing private asset lab.'
    }

    $manifestDirectory = Join-Path $assetLab 'manifests'
    $manifest = @(Get-ChildItem -LiteralPath $manifestDirectory -Filter 'H3VRFull-export-files-*.sha256.tsv' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1)[0]
    if ($null -eq $manifest) {
        throw 'Private asset lab has no H3VRFull SHA-256 manifest.'
    }

    $expectedName = if ($PrefabName.EndsWith('.prefab', [System.StringComparison]::OrdinalIgnoreCase)) { $PrefabName } else { "$PrefabName.prefab" }
    $prefabEntries = @(
        Get-Content -LiteralPath $manifest.FullName |
            ForEach-Object {
                $columns = $_ -split "`t", 2
                if ([System.IO.Path]::GetFileName($columns[0]).Equals($expectedName, [System.StringComparison]::OrdinalIgnoreCase)) {
                    $columns[0]
                }
            }
    )
    if ($prefabEntries.Count -eq 0) {
        throw "No archived prefab named '$expectedName' was found."
    }
    if ($prefabEntries.Count -gt 1) {
        throw "Archived prefab name '$expectedName' is ambiguous. Use a unique prefab name."
    }

    $resolvedRoot = Resolve-PrivateAssetRipSourceFile -AssetLab $assetLab -ManifestSourcePath $prefabEntries[0]
    $rootPath = $resolvedRoot.SourcePath
    $assetsRoot = $resolvedRoot.AssetsRoot

    $guidToAssetPath = @{}
    foreach ($meta in Get-ChildItem -LiteralPath $assetsRoot -Filter '*.meta' -File -Recurse) {
        $guidMatch = Select-String -LiteralPath $meta.FullName -Pattern '^guid:\s*([0-9a-f]{32})\s*$' -CaseSensitive -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $guidMatch) {
            $guidToAssetPath[$guidMatch.Matches[0].Groups[1].Value] = $meta.FullName.Substring(0, $meta.FullName.Length - 5)
        }
    }

    $pending = [System.Collections.Generic.Queue[string]]::new()
    $pending.Enqueue($rootPath)
    $visited = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $unresolved = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    while ($pending.Count -gt 0) {
        $assetPath = $pending.Dequeue()
        if (-not $visited.Add($assetPath)) {
            continue
        }
        try {
            $content = Get-Content -LiteralPath $assetPath -Raw -ErrorAction Stop
        }
        catch {
            continue
        }
        foreach ($reference in [regex]::Matches($content, 'guid:\s*([0-9a-f]{32})')) {
            $guid = $reference.Groups[1].Value
            if ($guidToAssetPath.ContainsKey($guid)) {
                $pending.Enqueue($guidToAssetPath[$guid])
            }
            else {
                $unresolved.Add($guid) | Out-Null
            }
        }
    }

    $rootOffset = $rootPath.IndexOf('\Assets\', [System.StringComparison]::OrdinalIgnoreCase)
    if ($rootOffset -lt 0) {
        $rootOffset = $rootPath.IndexOf('/Assets/', [System.StringComparison]::OrdinalIgnoreCase)
    }
    $safeRoot = $rootPath.Substring($rootOffset + 1)
    Write-Host "Asset graph root: $safeRoot"
    foreach ($assetPath in $visited | Sort-Object) {
        if ($assetPath -eq $rootPath) {
            continue
        }
        $assetOffset = $assetPath.IndexOf('\Assets\', [System.StringComparison]::OrdinalIgnoreCase)
        if ($assetOffset -lt 0) {
            $assetOffset = $assetPath.IndexOf('/Assets/', [System.StringComparison]::OrdinalIgnoreCase)
        }
        $safePath = if ($assetOffset -ge 0) { $assetPath.Substring($assetOffset + 1) } else { Split-Path -Leaf $assetPath }
        $kind = if ($safePath -match '(^|[\\/])Mesh([\\/]|$)') { 'Mesh' }
        elseif ($safePath -match '(^|[\\/])Material([\\/]|$)') { 'Material' }
        elseif ($safePath -match '(^|[\\/])Texture2D([\\/]|$)') { 'Texture2D' }
        elseif ($safePath -match '(^|[\\/])Shaders?([\\/]|$)') { 'Shader' }
        elseif ($safePath -match '(^|[\\/])MonoBehaviour([\\/]|$)') { 'Serialized component' }
        else { 'Asset' }
        Write-Host "Dependency: $kind | $safePath"
    }
    Write-Host "Graph nodes: $($visited.Count); unresolved GUIDs: $($unresolved.Count)."
}

function Assert-RemoteVersionIsNew {
    param(
        [object]$ModConfig,
        [string]$Version
    )

    $versionUrl = "https://thunderstore.io/c/$($ModConfig.community)/p/$($ModConfig.namespace)/$($ModConfig.packageName)/v/$Version/"
    $statusCode = & curl.exe -sS -o NUL -w '%{http_code}' --max-time 15 $versionUrl
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to query Thunderstore version metadata for $($ModConfig.namespace)-$($ModConfig.packageName)-$Version."
    }

    if ($statusCode -eq '404') {
        return
    }
    if ($statusCode -eq '200') {
        throw "Thunderstore already has $($ModConfig.namespace)-$($ModConfig.packageName)-$Version."
    }

    throw "Thunderstore version metadata request returned HTTP $statusCode for $($ModConfig.namespace)-$($ModConfig.packageName)-$Version."
}

function Invoke-Publish {
    param([object]$ModConfig)

    if (-not $Publish -or -not $VrApproved) {
        throw 'Publishing requires both -Publish and -VrApproved.'
    }

    if ((Get-ModProjectDirectory $ModConfig) -eq 'GunGameProgressions' -and $GunGameBuildConfiguration -ne 'Release') {
        throw 'GunGame Debug packages are local-only and cannot be published.'
    }

    $version = Get-ProjectVersion $ModConfig
    $artifactDirectory = Join-Path (Join-Path (Join-Path $BuildRoot 'artifacts') $Mod) $version
    $zipPath = @(Get-ChildItem -LiteralPath $artifactDirectory -Filter '*.zip' -File | Sort-Object LastWriteTime -Descending)[0].FullName
    Write-Host 'Publishing under explicit -Publish -VrApproved authorization; VR receipt status is not a publish gate.'

    Assert-RemoteVersionIsNew -ModConfig $ModConfig -Version $version

    $token = [Environment]::GetEnvironmentVariable('TCLI_AUTH_TOKEN', 'User')
    if ([string]::IsNullOrWhiteSpace($token) -and (Get-Command Get-StoredCredential -ErrorAction SilentlyContinue)) {
        try {
            $credential = Get-StoredCredential -Target 'H3VRMods:Thunderstore'
            if ($null -ne $credential) {
                $token = $credential.Password
            }
        }
        catch {
            Write-Verbose 'Thunderstore Credential Manager lookup was unavailable in this session.'
        }
    }
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw 'No Thunderstore publish token is configured. Run SetPublishToken first.'
    }

    Push-Location $RepoRoot
    try {
        Write-Host 'Restoring the Thunderstore CLI tool.'
        Invoke-CheckedNative { & dotnet tool restore }
        $tomlPath = Join-Path $artifactDirectory 'thunderstore.toml'
        @"
[package]
namespace = "$($ModConfig.namespace)"
name = "$($ModConfig.packageName)"
versionNumber = "$version"

[publish]
repository = "https://thunderstore.io"
communities = ["$($ModConfig.community)"]
"@ | Set-Content -LiteralPath $tomlPath -Encoding UTF8
        $env:TCLI_AUTH_TOKEN = $token
        Write-Host "Publishing $(Split-Path -Leaf $zipPath) with Thunderstore CLI."
        Invoke-CheckedNative { & dotnet tool run tcli -- publish --file $zipPath --config-path $tomlPath }
    }
    finally {
        Remove-Item Env:TCLI_AUTH_TOKEN -ErrorAction SilentlyContinue
        Pop-Location
    }
}

switch ($Action) {
    'Preflight' { Invoke-Preflight }
    'SourceStatus' { Get-SourceStatus | Format-List }
    'RefreshSource' { Invoke-RefreshSource }
    'FindType' { if ([string]::IsNullOrWhiteSpace($Query)) { throw 'FindType requires -Query.' }; Find-SourceType $Query }
    'FindMethod' {
        if ([string]::IsNullOrWhiteSpace($Query) -or $Query -notmatch '^(.+)\.([^.]+)$') { throw 'FindMethod requires Type.Method in -Query.' }
        $typeName = $Matches[1]
        $methodName = $Matches[2]
        $sourceMatches = Find-SourceType $typeName
        Select-String -Path ($sourceMatches.Path | Sort-Object -Unique) -Pattern ("\b" + [regex]::Escape($methodName) + "\s*\(")
    }
    'GrepSource' { if ([string]::IsNullOrWhiteSpace($Query)) { throw 'GrepSource requires -Query.' }; Find-SourceText $Query }
    'PrepareUnitySourceSync' { Prepare-UnityProjectSourceSync $Query }
    'SyncUnitySource' { Sync-UnityProjectSource $Query }
    'AuditItemId' { Find-InstalledItemId $Query }
    'AssetRipStatus' { Get-PrivateAssetArchiveStatus }
    'FindAssetRip' { Find-PrivateAssetRip $Query }
    'InspectAssetRip' { Get-PrivateAssetRipGraph $Query }
    'UnityBuildStatus' { Get-UnityBuildStatus (Get-ModConfig $Mod) }
    'Verify' { Assert-CurrentSource; $modConfig = Get-ModConfig $Mod; Assert-PatchTargets $modConfig; Assert-ExternalPatchTargets $modConfig; Write-Host "Verified $Mod." }
    'Build' {
        $modConfig = Get-ModConfig $Mod
        Invoke-Build -ModConfig $modConfig -BuildConfiguration (Get-EffectiveBuildConfiguration -ModConfig $modConfig)
    }
    'Test' { Invoke-Test }
    'Package' { New-Package (Get-ModConfig $Mod) | Format-List }
    'Deploy' { Invoke-Deploy (Get-ModConfig $Mod) }
    'Logs' { Invoke-LogAction 'all' }
    'TailLogs' { Invoke-LogAction 'tail' }
    'ClearLogs' { Invoke-LogAction 'clear' }
    'SetPublishToken' {
        $secureToken = Read-Host -Prompt 'Thunderstore service-account token' -AsSecureString
        $tokenPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
        try {
            $token = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($tokenPointer)
            [Environment]::SetEnvironmentVariable('TCLI_AUTH_TOKEN', $token, 'User')
            Write-Host 'Thunderstore publish token stored for the current Windows user.'
        }
        finally {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPointer)
        }
    }
    'Publish' { Invoke-Publish (Get-ModConfig $Mod) }
}
