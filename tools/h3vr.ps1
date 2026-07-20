[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Preflight', 'SourceStatus', 'RefreshSource', 'FindType', 'FindMethod', 'GrepSource', 'Verify', 'Build', 'Test', 'Package', 'Deploy', 'Logs', 'TailLogs', 'ClearLogs', 'SetPublishToken', 'Publish')]
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
        [int]$StartupTimeoutSeconds = 5,
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
        Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue
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
        if ($process.ExitCode -ne 0) {
            if (-not (Wait-ForUnityProjectBatchWorker -ProjectRoot $projectRoot)) {
                throw "Unity batch build failed with exit code $($process.ExitCode). See $logPath"
            }
            Write-Warning "Unity launcher exited with code $($process.ExitCode), but detached batch worker completed."
        }

        if ((Test-Path -LiteralPath $logPath) -and
            (Select-String -LiteralPath $logPath -Pattern $ModConfig.unityBuildSuccessMarker -SimpleMatch -Quiet)) {
            $buildCompleted = $true
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
        Test-R2modmanLocalPackageYamlEntry
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

function ConvertTo-YamlQuotedScalar {
    param([string]$Value)

    if ($null -eq $Value) {
        $Value = ''
    }

    return "'" + ($Value -replace "'", "''") + "'"
}

function New-R2modmanLocalPackageYamlEntry {
    param([object]$LocalManifest)

    $packageName = [string]$LocalManifest.name
    $quotedName = ConvertTo-YamlQuotedScalar $packageName
    $dependencies = @($LocalManifest.dependencies)
    $dependencyLines = if ($dependencies.Count -eq 0) {
        @('  dependencies: []')
    }
    else {
        @('  dependencies:') + @($dependencies | ForEach-Object {
            ('    - {0}' -f (ConvertTo-YamlQuotedScalar $_))
        })
    }

    $lines = @(
        '- manifestVersion: 2'
        ('  name: {0}' -f $quotedName)
        ('  authorName: {0}' -f (ConvertTo-YamlQuotedScalar $LocalManifest.authorName))
        ('  websiteUrl: {0}' -f (ConvertTo-YamlQuotedScalar $LocalManifest.websiteUrl))
        ('  displayName: {0}' -f (ConvertTo-YamlQuotedScalar $LocalManifest.displayName))
        ('  description: {0}' -f (ConvertTo-YamlQuotedScalar $LocalManifest.description))
        "  gameVersion: ''"
        "  networkMode: ''"
        "  packageType: ''"
        "  installMode: ''"
        ('  installedAtTime: {0}' -f $LocalManifest.installedAtTime)
        '  loaders: []'
    ) + $dependencyLines + @(
        '  incompatibilities: []'
        '  optionalDependencies: []'
        '  versionNumber:'
        ('    major: {0}' -f $LocalManifest.versionNumber.major)
        ('    minor: {0}' -f $LocalManifest.versionNumber.minor)
        ('    patch: {0}' -f $LocalManifest.versionNumber.patch)
        '  enabled: true'
        '  onlineSource: false'
    )

    return [string]::Join("`r`n", [string[]]$lines)
}

function Remove-R2modmanLocalPackageYamlEntries {
    param(
        [string]$Yaml,
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Yaml)) {
        return ''
    }

    $entryStarts = [regex]::Matches($Yaml, '(?m)^-\s+manifestVersion:\s*\d+\s*(?:\r?\n|$)')
    $output = New-Object System.Text.StringBuilder
    $cursor = 0
    for ($index = 0; $index -lt $entryStarts.Count; $index++) {
        $start = $entryStarts[$index].Index
        $end = if ($index + 1 -lt $entryStarts.Count) { $entryStarts[$index + 1].Index } else { $Yaml.Length }
        $entry = $Yaml.Substring($start, $end - $start)
        if ($entry -match [regex]::Escape($Name)) {
            [void]$output.Append($Yaml.Substring($cursor, $start - $cursor))
            $cursor = $end
        }
    }

    [void]$output.Append($Yaml.Substring($cursor))
    return $output.ToString().Trim()
}

function Test-R2modmanLocalPackageYamlEntry {
    $name = 'HLin_Mods-GunGame_Cursed_Random'
    $localManifest = [pscustomobject]@{
        name = $name
        authorName = 'HLin_Mods'
        websiteUrl = ''
        displayName = 'GunGame_Cursed_Random'
        description = 'test'
        installedAtTime = 0
        dependencies = @('Kodeman-GunGame-1.0.2')
        versionNumber = [pscustomobject]@{ major = 1; minor = 0; patch = 0 }
    }
    $entry = New-R2modmanLocalPackageYamlEntry $localManifest
    $quotedName = ConvertTo-YamlQuotedScalar $name
    if ($entry -notmatch ('(?m)^  name: ' + [regex]::Escape($quotedName) + '\r?$')) {
        throw 'r2modman YAML entry must keep name and scalar on one line.'
    }
    if ($entry -match '(?m)^  name:\s*$') {
        throw 'r2modman YAML entry must not split name from its scalar.'
    }

    $brokenEntry = "- manifestVersion: 2`r`n  name:`r`n$quotedName`r`n  onlineSource: false"
    $remaining = Remove-R2modmanLocalPackageYamlEntries -Yaml ("- manifestVersion: 2`r`n  name: 'Other-Mod'`r`n  onlineSource: false`r`n" + $brokenEntry) -Name $name
    if ($remaining -match [regex]::Escape($name) -or $remaining -notmatch 'Other-Mod') {
        throw 'r2modman YAML repair must remove only the target malformed entry.'
    }
}

function Set-R2modmanLocalPackageYamlEntry {
    param(
        [string]$ModsPath,
        [string]$Existing,
        [string]$Name,
        [string]$Entry
    )

    $validEntryPattern = '(?ms)^-\s+manifestVersion:\s*2\s*\r?\n\s+name:\s*["'']?' + [regex]::Escape($Name) + '["'']?\s*\r?$'
    if ($Existing -match $validEntryPattern) {
        return
    }

    $remaining = Remove-R2modmanLocalPackageYamlEntries -Yaml $Existing -Name $Name
    $updated = if ([string]::IsNullOrWhiteSpace($remaining) -or $remaining -eq '[]') {
        $Entry
    }
    else {
        $remaining + "`r`n" + $Entry
    }

    $tempPath = $ModsPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    [System.IO.File]::WriteAllText($tempPath, $updated, [System.Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $ModsPath) {
        $backupPath = $ModsPath + '.before-cursed-random-' + (Get-Date).ToString('yyyyMMdd-HHmmss') + '.bak'
        [System.IO.File]::Replace($tempPath, $ModsPath, $backupPath, $true)
    }
    else {
        [System.IO.File]::Move($tempPath, $ModsPath)
    }
}

function Register-R2modmanLocalPackage {
    param(
        [object]$ModConfig,
        [string]$PackageRoot
    )

    if (-not $ModConfig.registerWithR2modman) {
        return
    }

    $manifestPath = Join-Path $PackageRoot 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "Cannot register r2modman local package: missing manifest.json for $Mod."
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($manifest.author) -or [string]::IsNullOrWhiteSpace($manifest.name) -or
        [string]::IsNullOrWhiteSpace($manifest.version_number)) {
        throw "Cannot register r2modman local package: manifest requires author, name, and version_number."
    }

    $name = "$($manifest.author)-$($manifest.name)"
    if ($ModConfig.deploymentFolder -ne $name) {
        throw "Cannot register r2modman local package: deploymentFolder must equal manifest package name '$name'."
    }

    $versionMatch = [regex]::Match([string]$manifest.version_number, '^(\d+)\.(\d+)\.(\d+)$')
    if (-not $versionMatch.Success) {
        throw "Cannot register r2modman local package: version_number must be major.minor.patch."
    }

    $profileRoot = Split-Path (Split-Path $EnvironmentConfig.r2modman.pluginsRoot -Parent) -Parent
    $cachePath = Join-Path (Join-Path (Join-Path $profileRoot 'cache') $name) $manifest.version_number
    Remove-Item -LiteralPath $cachePath -Recurse -Force -ErrorAction SilentlyContinue
    Ensure-Directory $cachePath
    Copy-Item -Path (Join-Path $PackageRoot '*') -Destination $cachePath -Recurse -Force

    $localManifest = [ordered]@{
        manifestVersion = 2
        name = $name
        authorName = $manifest.author
        websiteUrl = $manifest.website_url
        displayName = $manifest.name
        description = $manifest.description
        gameVersion = ''
        networkMode = ''
        packageType = ''
        installMode = ''
        installedAtTime = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
        loaders = @()
        dependencies = @($manifest.dependencies)
        incompatibilities = @()
        optionalDependencies = @()
        versionNumber = [ordered]@{
            major = [int]$versionMatch.Groups[1].Value
            minor = [int]$versionMatch.Groups[2].Value
            patch = [int]$versionMatch.Groups[3].Value
        }
        enabled = $true
        onlineSource = $false
    }
    Write-JsonFile -Path (Join-Path $cachePath 'mm_v2_manifest.json') -Value $localManifest

    $modsPath = Join-Path $profileRoot 'mods.yml'
    $existing = if (Test-Path -LiteralPath $modsPath) { Get-Content -LiteralPath $modsPath -Raw } else { '' }
    $entry = New-R2modmanLocalPackageYamlEntry $localManifest
    Set-R2modmanLocalPackageYamlEntry -ModsPath $modsPath -Existing $existing -Name $name -Entry $entry

    Write-Host "Registered $name as an r2modman local package."
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

    Register-R2modmanLocalPackage -ModConfig $ModConfig -PackageRoot $deployStaging

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
