[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Preflight', 'SourceStatus', 'RefreshSource', 'FindType', 'FindMethod', 'GrepSource', 'Verify', 'Build', 'Test', 'Package', 'Deploy', 'Logs', 'TailLogs', 'ClearLogs', 'SetPublishToken', 'Publish')]
    [string]$Action,

    [ValidateSet('ThePing', 'GunGameProgressions', 'Teleport', 'RemoveWhiteOut')]
    [string]$Mod = 'ThePing',

    [string]$Query,
    [switch]$Publish,
    [switch]$VrApproved
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$BuildRoot = Join-Path $RepoRoot 'build'
$PublicEnvironmentConfigPath = Join-Path $BuildRoot 'environment.json'
$LocalEnvironmentConfigPath = Join-Path $BuildRoot 'environment.local.json'
$EnvironmentConfigPath = if (Test-Path -LiteralPath $LocalEnvironmentConfigPath) {
    $LocalEnvironmentConfigPath
}
else {
    $PublicEnvironmentConfigPath
}
$EnvironmentConfig = Get-Content -LiteralPath $EnvironmentConfigPath -Raw | ConvertFrom-Json
$ModsConfig = Get-Content -LiteralPath (Join-Path $BuildRoot 'mods.json') -Raw | ConvertFrom-Json

function Expand-EnvironmentConfiguration {
    param([object]$Config)

    foreach ($section in @($Config.h3vr, $Config.r2modman)) {
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

    $manifestPath = Join-Path (Join-Path $RepoRoot $ModConfig.packageSource) 'manifest.json'
    return (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).version_number
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

function Get-GunGameStagingPath {
    return Join-Path (Join-Path $BuildRoot 'staging') 'GunGameProgressions-generator'
}

function Test-GunGamePools {
    param([string]$Path)

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
            if ([string]::IsNullOrWhiteSpace($gun.GunName) -or
                [string]::IsNullOrWhiteSpace($gun.MagName) -or
                @($gun.MagNames).Count -eq 0 -or
                $gun.CategoryID -notin @(0, 1, 2) -or
                $gun.MagName -notin @($gun.MagNames)) {
                throw "Invalid GunGame advanced weapon: $($pool.Name) / $($gun.GunName)"
            }
        }
    }
}

function Invoke-GunGameBuild {
    param([object]$ModConfig)

    $stagingPath = Get-GunGameStagingPath
    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
    Ensure-Directory $stagingPath

    Invoke-CheckedNative { & dotnet build (Join-Path $RepoRoot $ModConfig.metadataExporterCsproj) -c Release }

    $sourcePath = Join-Path $RepoRoot 'GunGameProgressions'
    $profileSourcePath = Join-Path $RepoRoot $ModConfig.profileSource
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
    $runtimeMetadataPath = Join-Path (Join-Path $EnvironmentConfig.r2modman.pluginsRoot $ModConfig.deploymentFolder) 'ObjectData.json'
    if (-not (Test-Path -LiteralPath $runtimeMetadataPath) -or (Get-Item -LiteralPath $runtimeMetadataPath).Length -le 2) {
        Write-Host "No runtime GunGame metadata export was found; using tracked GunGame profile assets."
        Copy-Item -LiteralPath (Join-Path $sourcePath 'profile-rules.json') -Destination $stagingPath
        $offlinePoolPaths | ForEach-Object { Copy-Item -LiteralPath $_ -Destination $stagingPath }
        Test-GunGamePools $stagingPath
        return $stagingPath
    }

    $metadataPath = $runtimeMetadataPath
    if ((Test-Path -LiteralPath $runtimeMetadataPath) -and (Get-Item -LiteralPath $runtimeMetadataPath).Length -gt 2) {
        Write-Host "Using metadata exported by the installed GunGame package: $metadataPath"
    }

    Copy-Item -LiteralPath $metadataPath -Destination (Join-Path $stagingPath 'ObjectData.json')
    Copy-Item -LiteralPath (Join-Path $sourcePath 'jsonGen.py') -Destination $stagingPath
    Copy-Item -LiteralPath (Join-Path $sourcePath 'profile-rules.json') -Destination $stagingPath
    Push-Location $stagingPath
    try {
        Invoke-CheckedNative {
            & python .\jsonGen.py 0 --seed 0 --rules .\profile-rules.json `
                --output-name 'GunGameWeaponPool_Runtime_01_Vanilla_Rot_RW_Rot.json' `
                --profile-name 'Runtime 01 - Vanilla Rot' `
                --description 'A Rot-only random progression using active vanilla firearms.' `
                --enemy-types 'RW_Rot' `
                --enemy-progression-type 0 `
                --order-type 1
        }
        Invoke-CheckedNative {
            & python .\jsonGen.py 0 --seed 0 --rules .\profile-rules.json `
                --output-name 'GunGameWeaponPool_Runtime_03_Vanilla_Mixed_Enemy_RW_Rot.json' `
                --profile-name 'Runtime 03 - Vanilla Mixed Enemy' `
                --description 'A weighted mixed-enemy progression using active vanilla firearms.' `
                --enemy-types 'RW_Rot,M_Swat_Scout,M_MercWiener_Riflewiener,M_Swat_SpecOps,M_Swat_Heavy' `
                --enemy-values '8,5,3,2,1' `
                --enemy-progression-type 0 `
                --order-type 1
        }
    }
    finally {
        Pop-Location
    }

    Test-GunGamePools $stagingPath
    return $stagingPath
}

function Invoke-Build {
    param([object]$ModConfig)

    if ($ModConfig.kind -eq 'dotnet') {
        Invoke-DotNetBuild $ModConfig
        return
    }

    if ($ModConfig.kind -eq 'python') {
        Invoke-GunGameBuild $ModConfig | Out-Null
        return
    }

    throw "Unsupported build kind: $($ModConfig.kind)"
}

function Invoke-Test {
    Invoke-CheckedNative { & dotnet test (Join-Path $RepoRoot 'tests\H3vrPipeline.Tests\H3vrPipeline.Tests.csproj') }
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
        [string]$PackageRoot
    )

    foreach ($payload in @($ModConfig.payload)) {
        if ($payload.from -eq 'generated') {
            $source = Get-GunGameStagingPath
        }
        else {
            $source = Join-Path $RepoRoot $payload.from
        }

        if (-not (Test-Path -LiteralPath $source)) {
            throw "Package payload does not exist: $source"
        }

        if ((Get-Item -LiteralPath $source).PSIsContainer) {
            $files = if ($payload.from -eq 'generated') {
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

function New-Package {
    param([object]$ModConfig)

    Invoke-Build $ModConfig | Out-Null
    $version = Get-ProjectVersion $ModConfig
    $artifactDirectory = Join-Path (Join-Path (Join-Path $BuildRoot 'artifacts') $Mod) $version
    $stagingDirectory = Join-Path (Join-Path $BuildRoot 'staging') ("package-" + $Mod)
    $packageRoot = Join-Path $stagingDirectory 'payload'
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Ensure-Directory $packageRoot

    Copy-PackageMetadata -ModConfig $ModConfig -PackageRoot $packageRoot -Version $version
    Copy-Payload -ModConfig $ModConfig -PackageRoot $packageRoot

    Ensure-Directory $artifactDirectory
    $zipPath = Join-Path $artifactDirectory ("$($ModConfig.namespace)-$($ModConfig.packageName)-$version.zip")
    Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $zipPath)

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
        createdAt = (Get-Date).ToUniversalTime().ToString('o')
    })

    return [PSCustomObject]@{ Version = $version; ZipPath = $zipPath; Sha256 = Get-FileSha256 $zipPath }
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
    'Verify' { Assert-CurrentSource; Assert-PatchTargets (Get-ModConfig $Mod); Write-Host "Verified $Mod." }
    'Build' { Invoke-Build (Get-ModConfig $Mod) }
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
