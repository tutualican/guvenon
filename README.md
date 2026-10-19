# Guven-ON

Quiet protection for untrusted networks.

[Download the preview](https://github.com/tutualican/guvenon/releases/latest) ·
[Website](https://tutualican.github.io/guvenon/)

Guven-ON is a network security tool designed to block DNS leaks and local-network
exposure on public Wi-Fi. It currently targets Windows 11; a Debian/Ubuntu Linux
build is planned. It is designed to live in the background, start with the
system, and ask for nothing after installation. The user interface is available
in English, German and Turkish.

> **Status: early preview.** The tray app, Windows service, installer, update
> check and website are in place. Packet filtering (WFP) and DNS policy (NRPT)
> enforcement are the next implementation phase. This preview does not protect
> traffic yet. Linux support is planned as a Debian/Ubuntu `.deb`; no Linux
> build is available today.

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
tests/GuvenOn.Tests  unit tests for versioning, updates, settings and autostart
installer/           Inno Setup script
winget/              WinGet manifests for 0.1.2
docs/                static GitHub Pages site
scripts/             icon and update-manifest helpers
.github/workflows/   release and Pages pipelines
```

## Build

Requirements:

- Windows 11 22H2 or newer, x64
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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release -Runtime win-x64 -SelfContained
```

Build the installer and `latest.json` as well:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release -Runtime win-x64 -SelfContained -Installer
```

The outputs are written to `artifacts/publish/`, `artifacts/installer/` and
`artifacts/latest.json`.

Run the unit tests:

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
dotnet test GuvenOn.slnx -c Release -warnaserror
```

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
https://github.com/tutualican/guvenon/releases/latest/download/latest.json
```

That path is a release-asset redirect, not the REST API. It needs no token and
does not consume the 60-requests-per-hour unauthenticated API budget. A CDN
mirror at `https://dl.guvenon.app/win/latest.json` is tried only if GitHub is
unreachable.

Each release must upload `latest.json` next to the installer. The helper
generates it with the immutable asset URL, size and SHA-256:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\make-manifest.ps1 -InstallerPath .\artifacts\installer\GuvenOnSetup-0.1.2.exe -Version 0.1.2
```

The app verifies the version and offers a download link from the manifest. The
manifest signature (ECDSA P-256) and silent install/rollback are wired in only
after signing keys exist. Preview builds are unsigned on purpose.

The repository constants live in `src/GuvenOn.Core/AppInfo.cs`
(`GitHubOwner`, `GitHubRepo`) and `docs/assets/app.js` (`REPO`). They currently
point at `tutualican/guvenon`.

## Installer

`installer/GuvenOn.iss` installs:

- the tray app under `{autopf}\Guven-ON`
- the service under `{app}\service`
- the service itself through `guvenonsvc.exe --install`
- optional sign-in autostart (per-user) and desktop shortcut
- an uninstaller that stops and removes the service, deletes
  `%ProgramData%\GuvenOn`, and runs the cleanup helper for the current profile
  to remove its settings and startup entry

The installer requires administrator rights once. The app manifest is
`asInvoker`, so day-to-day use never prompts for elevation. Uninstall also runs
elevated through Windows' standard uninstall flow.

## WinGet

Versioned manifests for `Tutualican.GuvenOn` live in
`winget/manifests/t/Tutualican/GuvenOn/0.1.2/`. They describe the per-machine
Inno installer, silent switches and English, German and Turkish descriptions.
Validate them locally with:

```powershell
winget validate --manifest .\winget\manifests\t\Tutualican\GuvenOn\0.1.2
```

Submitting to the community `winget-pkgs` repository is a separate review step;
the manifests in this repository are ready for that submission.

## Website

Live site: <https://tutualican.github.io/guvenon/>

The site is a dependency-free static page in `docs/`:

- `docs/index.html`
- `docs/404.html`, `docs/robots.txt` and `docs/sitemap.xml`
- `docs/assets/styles.css`
- `docs/assets/app.js`
- `docs/assets/guvenon.png` (generated from the shield artwork)
- `docs/assets/og.png` (social preview card)

It is deployed by `.github/workflows/pages.yml` to GitHub Pages at
<https://tutualican.github.io/guvenon/>. The repository uses
**Settings → Pages → Build and deployment → Source: GitHub Actions**. If you
prefer branch deployment, choose `main` and `/docs`; the site does not require a
build step.

The language control starts in **AUTO**, follows the browser/`Intl` language,
and stores a manual choice in `localStorage`. You can force a language with
`?lang=en`, `?lang=de`, `?lang=tr` or `?lang=auto`. All download links point at
`https://github.com/tutualican/guvenon/releases/latest`.

The visual language is derived from the shield artwork: warm cream paper, deep
navy ink, soft blue highlights and restrained gold accents.

## Icon

The supplied shield artwork is cleaned by `scripts/make-icon.ps1`. The generated
assets are:

- `src/GuvenOn.App/Assets/Guven-ON.png` (cleaned, transparent)
- `src/GuvenOn.App/Assets/guvenon.ico` (16–256 px)
- `docs/assets/guvenon.png` (256 px site icon)
- `docs/assets/og.png` (social preview card)

Regenerate them after changing the artwork:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\make-icon.ps1
```

The script removes black or near-white corners with a flood fill and preserves
the light details inside the shield.

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

The detailed, research-backed backlog lives in [ROADMAP.md](ROADMAP.md).

1. Real protection phase: trust-aware profiles, DNS/DoH guard, LAN exposure
   guard, IPv6 guard, fail-closed kill switch, captive-portal mode, panic and
   self-test.
2. Linux target: Debian/Ubuntu `.deb` with a systemd service, PolicyKit, D-Bus,
   nftables and systemd-resolved/NetworkManager integration.
3. WireGuard, split tunneling, per-app rules, visibility and threat alerts.
4. Signed update manifest, silent installer handoff, rollback.
5. WinGet manifest submission (manifests are kept in `winget/`).
6. Code signing and SmartScreen reputation.

Linux packaging starts with `.deb`; Flatpak/AppImage are not a substitute for a
system-wide firewall/DNS daemon.

## License

Apache License 2.0. See [LICENSE](LICENSE).

Guven-ON is an independent project and is not affiliated with Microsoft.
