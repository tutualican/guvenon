# Guven-ON

Quiet protection for untrusted networks.

Guven-ON is a Windows security tool designed to block DNS leaks and local-network
exposure on public Wi-Fi. It is designed to live in the background, start with
Windows, and ask for nothing after installation. The user interface is available
in English, German and Turkish.

> **Status: early preview.** The tray app, Windows service, installer, update
> check and website are in place. Packet filtering (WFP) and DNS policy (NRPT)
> enforcement are the next implementation phase. This preview does not protect
> traffic yet.

## What works today

- WPF tray app (`guvenon.exe`) with a calm, Apple-inspired interface.
- Windows service (`guvenonsvc.exe`, `GuvenOnSvc`) with automatic recovery.
- One UAC prompt at install time; the tray app runs as `asInvoker` and never
  elevates for normal operations.
- English, German and Turkish UI resources with live language switching.
- Per-user autostart through `HKCU\...\Run`; the service starts at boot.
- Update check through GitHub Releases without the GitHub API.
- Inno Setup installer and uninstaller for a per-machine install.
- Static GitHub Pages site in `docs/` with the same three languages.
- Apache-2.0 license.

## Repository layout

```
src/GuvenOn.App      WPF tray app and UI resources
src/GuvenOn.Core     settings, autostart, SemVer, update manifest
src/GuvenOn.Service  Windows service and service installer
installer/           Inno Setup script
docs/                static GitHub Pages site
scripts/             icon and update-manifest helpers
.github/workflows/   release and Pages pipelines
```

## Build

Requirements:

- Windows 10/11 x64
- .NET 10 SDK
- Inno Setup 6 for the installer step

The SDK can be installed without administrator rights:

```powershell
$installer = "$env:TEMP\dotnet-install.ps1"
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $installer
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Channel 10.0
```

Build and publish a self-contained build:

```powershell
.\build.ps1 -Configuration Release -Runtime win-x64 -SelfContained
```

Build the installer and `latest.json` as well:

```powershell
.\build.ps1 -Configuration Release -Runtime win-x64 -SelfContained -Installer
```

The outputs are written to `artifacts/publish/`, `artifacts/installer/` and
`artifacts/latest.json`.

For local development, install the Windows Desktop runtime so the apphost can
find it. When the SDK is user-local, start the app with `DOTNET_ROOT` set:

```powershell
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
.\src\GuvenOn.App\bin\Debug\net10.0-windows\guvenon.exe
```

Run the service in console mode:

```powershell
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
.\src\GuvenOn.Service\bin\Debug\net10.0-windows\guvenonsvc.exe
```

Service installation is intentionally kept out of the app:

```powershell
# elevated prompt
.\artifacts\publish\svc\guvenonsvc.exe --install
.\artifacts\publish\svc\guvenonsvc.exe --uninstall
```

## Updates without the GitHub API

The app requests:

```
https://github.com/OWNER/REPO/releases/latest/download/latest.json
```

That path is a release-asset redirect, not the REST API. It needs no token and
does not consume the 60-requests-per-hour unauthenticated API budget. A CDN
mirror at `https://dl.guvenon.app/win/latest.json` is tried only if GitHub is
unreachable.

Each release must upload `latest.json` next to the installer. The helper
generates it with the immutable asset URL, size and SHA-256:

```powershell
.\scripts\make-manifest.ps1 -InstallerPath .\artifacts\installer\GuvenOnSetup-0.1.0.exe -Version 0.1.0
```

The app verifies the version and offers a download link from the manifest. The
manifest signature (ECDSA P-256) and silent install/rollback are wired in only
after signing keys exist. Preview builds are unsigned on purpose.

Before the first release, update the repository constants in
`src/GuvenOn.Core/AppInfo.cs` (`GitHubOwner`, `GitHubRepo`) and in
`docs/assets/app.js` (`REPO`).

## Installer

`installer/GuvenOn.iss` installs:

- the tray app under `{autopf}\Guven-ON`
- the service under `{app}\service`
- the service itself through `guvenonsvc.exe --install`
- optional sign-in autostart (per-user) and desktop shortcut
- an uninstaller that stops and removes the service and deletes
  `%ProgramData%\GuvenOn` and the current user's settings

The installer requires administrator rights once. The app manifest is
`asInvoker`, so day-to-day use never prompts for elevation. Uninstall also runs
elevated through Windows' standard uninstall flow.

## Website

The site is a dependency-free static page in `docs/`:

- `docs/index.html`
- `docs/assets/styles.css`
- `docs/assets/app.js`
- `docs/assets/guvenon.png` (generated from the desktop logo)
- `docs/assets/og.png` (social preview card)

It is deployed by `.github/workflows/pages.yml` to GitHub Pages. Enable
**Settings → Pages → Build and deployment → Source: GitHub Actions** in the
repository. If you prefer branch deployment, choose `main` and `/docs`; the
site does not require a build step.

The site uses English by default, auto-detects German and Turkish, and remembers
the choice in `localStorage`. All download links point at
`https://github.com/OWNER/REPO/releases/latest`.

## Icon

The source artwork is `Guven-ON.png` on the desktop. The generated assets are:

- `src/GuvenOn.App/Assets/Guven-ON.png` (cleaned, transparent)
- `src/GuvenOn.App/Assets/guvenon.ico` (16–256 px)
- `docs/assets/guvenon.png` (256 px site icon)

Regenerate them after changing the artwork:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\make-icon.ps1
```

The script flood-fills the semi-transparent white background and preserves the
white details inside the shield.

## Security notes

- The tray app runs with the user's normal token. It cannot change firewall or
  DNS policy by itself.
- The service runs as `LocalSystem` and is the only component that will own WFP
  filters and DNS policy. IPC commands are enumerated, never free-form.
- The service must remove persistent WFP filters on uninstall. That is a release
  blocker for the enforcement phase.
- No per-query DNS logging. The UI is driven by counts and state changes.
- Preview installers are unsigned until the code-signing account is active.
  Publish SHA-256 hashes and expect SmartScreen warnings in the meantime.

## Roadmap

1. WFP filters + NRPT/DoH policy, fail-closed only on untrusted profiles.
2. Captive-portal detection, LAN allowlist, panic reset and auto-revert.
3. Signed update manifest, silent installer handoff, rollback.
4. Windows 10 DNS proxy path.
5. WinGet manifests and automated release submission.
6. Code signing and SmartScreen reputation.

## License

Apache License 2.0. See [LICENSE](LICENSE).

Guven-ON is an independent project and is not affiliated with Microsoft.
