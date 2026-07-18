[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$AssetRipperExecutable,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$InputPath,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputPath,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$LogPath,

    [ValidateRange(1025, 65535)]
    [int]$Port = 31821,

    [ValidateRange(1, 120)]
    [int]$StartupTimeoutSeconds = 30,

    [ValidateRange(60, 7200)]
    [int]$ActionTimeoutSeconds = 7200
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Wait-AssetRipperServer {
    param(
        [string]$BaseUri,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            Invoke-WebRequest -UseBasicParsing -Uri $BaseUri -TimeoutSec 2 | Out-Null
            return
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }
    while ((Get-Date) -lt $deadline)

    throw 'AssetRipper did not start its local headless server before the timeout.'
}

$resolvedExecutable = (Resolve-Path -LiteralPath $AssetRipperExecutable -ErrorAction Stop).Path
$resolvedInput = (Resolve-Path -LiteralPath $InputPath -ErrorAction Stop).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputPath)
$stagingPath = $outputPath + '.exporting'
$logPath = [System.IO.Path]::GetFullPath($LogPath)

if (Test-Path -LiteralPath $outputPath) {
    throw "Refusing to overwrite existing AssetRipper export: $outputPath"
}

if (Test-Path -LiteralPath $stagingPath) {
    throw "Refusing to overwrite unfinished AssetRipper staging: $stagingPath"
}

if (Test-Path -LiteralPath $logPath) {
    throw "Refusing to overwrite existing AssetRipper log: $logPath"
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $logPath) | Out-Null

$baseUri = "http://127.0.0.1:$Port"
$process = $null

try {
    $process = Start-Process -FilePath $resolvedExecutable -ArgumentList @(
        '--headless=true',
        "--port=$Port",
        '--log=true',
        "--log-path=$logPath"
    ) -PassThru

    Wait-AssetRipperServer -BaseUri $baseUri -TimeoutSeconds $StartupTimeoutSeconds

    $loadEndpoint = if ((Get-Item -LiteralPath $resolvedInput).PSIsContainer) {
        "$baseUri/LoadFolder"
    }
    else {
        "$baseUri/LoadFile"
    }

    Invoke-WebRequest -UseBasicParsing -Method Post -Uri $loadEndpoint `
        -ContentType 'application/x-www-form-urlencoded' -Body @{ path = $resolvedInput } `
        -MaximumRedirection 5 -TimeoutSec $ActionTimeoutSeconds | Out-Null

    Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$baseUri/Export/UnityProject" `
        -ContentType 'application/x-www-form-urlencoded' -Body @{ path = $stagingPath } `
        -MaximumRedirection 5 -TimeoutSec $ActionTimeoutSeconds | Out-Null

    $projectVersion = Join-Path $stagingPath 'ExportedProject\\ProjectSettings\\ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $projectVersion)) {
        throw 'AssetRipper completed without creating Unity project metadata. Staging is preserved for review.'
    }

    Move-Item -LiteralPath $stagingPath -Destination $outputPath
    Write-Host "Private AssetRipper export completed: $outputPath"
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
