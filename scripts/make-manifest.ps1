param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Repo = "tutualican/guvenon",
    [string]$Channel = "stable",
    [string]$Output,
    [string]$Notes = "See the GitHub release notes."
)

$ErrorActionPreference = "Stop"

if (-not $Output) {
    $Output = Join-Path $PSScriptRoot "..\artifacts\latest.json"
}

$installer = Get-Item $InstallerPath
$tag = if ($Version.StartsWith("v")) { $Version } else { "v$Version" }
$cleanVersion = $tag.TrimStart("v")
$hash = (Get-FileHash $installer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$url = "https://github.com/$Repo/releases/download/$tag/$($installer.Name)"

$manifest = [ordered]@{
    channel    = $Channel
    version    = $cleanVersion
    minVersion = "0.1.0"
    url        = $url
    sha256     = $hash
    size       = $installer.Length
    rollout    = 100
    revoked    = @()
    published  = (Get-Date).ToUniversalTime().ToString("o")
    notes      = [ordered]@{
        en = $Notes
        de = $Notes
        tr = $Notes
    }
}

$directory = Split-Path -Parent $Output
if ($directory) {
    New-Item -ItemType Directory -Force $directory | Out-Null
}

$json = $manifest | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText($Output, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Wrote $Output"
Write-Host "SHA-256: $hash"
