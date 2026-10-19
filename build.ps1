param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$Installer
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

function Find-DotNet {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"),
        "C:\Program Files\dotnet\dotnet.exe",
        $(Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    throw "The .NET SDK was not found. Install .NET 10 SDK or set PATH."
}

function Find-InnoSetup {
    $candidates = @(
        $(Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    throw "Inno Setup 6 was not found. Install it from https://jrsoftware.org/isdl.php and rerun with -Installer."
}

$dotnet = Find-DotNet
$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

Write-Host "Using SDK: $dotnet"
Write-Host "Configuration: $Configuration"
Write-Host "Runtime: $Runtime"
Write-Host "Self-contained: $selfContainedValue"

Remove-Item "$root\artifacts\publish" -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$root\artifacts\publish\app" | Out-Null
New-Item -ItemType Directory -Force "$root\artifacts\publish\svc" | Out-Null

& $dotnet publish "$root\src\GuvenOn.App\GuvenOn.App.csproj" `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContainedValue `
    -warnaserror `
    -o "$root\artifacts\publish\app"

if ($LASTEXITCODE -ne 0) {
    throw "The tray app failed to publish."
}

& $dotnet publish "$root\src\GuvenOn.Service\GuvenOn.Service.csproj" `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContainedValue `
    -warnaserror `
    -o "$root\artifacts\publish\svc"

if ($LASTEXITCODE -ne 0) {
    throw "The service failed to publish."
}

Write-Host "Published app: $root\artifacts\publish\app"
Write-Host "Published service: $root\artifacts\publish\svc"

if ($Installer) {
    [xml]$props = Get-Content "$root\Directory.Build.props"
    $version = $props.Project.PropertyGroup.Version
    if (-not $version) {
        throw "Version is missing from Directory.Build.props."
    }

    $iscc = Find-InnoSetup
    Write-Host "Using Inno Setup: $iscc"

    & $iscc "/DAppVersion=$version" "$root\installer\GuvenOn.iss"
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed."
    }

    $installerPath = Join-Path $root "artifacts\installer\GuvenOnSetup-$version.exe"
    if (-not (Test-Path $installerPath)) {
        throw "Installer was not produced: $installerPath"
    }

    & "$root\scripts\make-manifest.ps1" -InstallerPath $installerPath -Version $version
    Write-Host "Installer: $installerPath"
    Write-Host "Manifest: $root\artifacts\latest.json"
}
