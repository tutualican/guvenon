# Guven-ON

Quiet protection for untrusted networks.

[Download the preview](https://github.com/tutualican/guvenon/releases/latest) ·
[Website](https://tutualican.github.io/guvenon/)

Guven-ON blocks DNS leaks and local-network exposure on public Wi-Fi. It targets
Windows 11 and now includes a Linux alpha `.deb`. It is designed to live in the
background, start with the system, and ask for nothing after installation. The
user interface is available in English, German and Turkish.

> **Status: early preview with opt-in enforcement.** The tray app, Windows
> service, WFP filters, trusted-DNS setup, installer, update check and website
> are in place. Protection is **off until you enable it**. On Windows the guards
> are dynamic WFP filters plus adapter DNS changes; on Linux they are an
> nftables table plus systemd-resolved configuration. Still missing: automatic
> trust profiles, a LAN allowlist, automatic captive-portal detection,
> DNS-over-HTTPS blocking, full IPv6 isolation and fail-closed behavior after a
> Windows service crash. Preview builds are unsigned.

## What works today

- WPF tray app (`guvenon.exe`) with a calm, Apple-inspired interface.
- Windows service (`guvenonsvc.exe`, `GuvenOnSvc`) with automatic recovery.
- Opt-in Windows enforcement: dynamic WFP session, trusted DNS on active
  adapters (Cloudflare and Quad9), plain DNS/DoT blocking, private IPv4 and ULA
  isolation, legacy discovery blocking, loopback/DHCP permits, and a
  ten-minute captive-portal pause with automatic resume.
- Linux alpha daemon (`guvenon-linuxd`) and `guvenonctl` CLI with an nftables
  `inet guvenon` table, systemd-resolved DNS configuration, state persistence,
  pause/resume and package cleanup.
- One UAC prompt at install time; the tray app runs as `asInvoker` and never
  elevates for normal operations.
- English, German and Turkish UI resources with live language switching.
- Per-user autostart through `HKCU\...\Run`; the service starts at boot.
- Update check through GitHub Releases without the GitHub API.
- Inno Setup installer and uninstaller for a per-machine install.
- Debian/Ubuntu `.deb` build script and GitHub Actions Linux job.
- Static GitHub Pages site in `docs/` with the same three languages.
- Apache-2.0 license.

## Repository layout

```
src/GuvenOn.App      WPF tray app and UI resources
src/GuvenOn.Core     shared policy, settings, autostart, SemVer, update manifest
src/GuvenOn.Service  Windows service, WFP engine, DNS configurator
src/GuvenOn.Linux    Linux daemon and guvenonctl CLI
tests/GuvenOn.Tests  unit tests for policy, WFP-facing rules, updates and settings
installer/           Inno Setup script
linux/               systemd unit, Debian control scripts and .deb builder
winget/              WinGet manifests (0.2.0 current)
docs/                static GitHub Pages site
scripts/             icon and update-manifest helpers
.github/workflows/   Windows/Linux release and Pages pipelines
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

## Linux alpha

The Linux package is built on Debian/Ubuntu or in the release workflow:

```bash
bash linux/build-deb.sh
```

It publishes a self-contained `linux-x64` binary and produces
`artifacts/linux/guvenon_<version>_amd64.deb`. The package installs:

- `/usr/lib/guvenon/guvenon-linuxd`
- `/usr/bin/guvenonctl` (symlink to the same binary)
- `/lib/systemd/system/guvenon.service`

After installing, enable protection explicitly:

```bash
sudo systemctl status guvenon
sudo guvenonctl status
sudo guvenonctl enable
sudo guvenonctl pause 10    # captive portal
sudo guvenonctl panic       # immediately restore DNS and remove filters
```

The daemon listens on a root-only Unix socket at `/run/guvenon/control.sock`.
It uses `nftables` and expects `systemd-resolved` for the cleanest DNS
integration; the fallback writes `/etc/resolv.conf`. A PolicyKit/D-Bus frontend,
NetworkManager integration and arm64 packaging are roadmap items.

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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\make-manifest.ps1 -InstallerPath .\artifacts\installer\GuvenOnSetup-0.2.0.exe -Version 0.2.0
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
`winget/manifests/t/Tutualican/GuvenOn/0.2.0/`. They describe the per-machine
Inno installer, silent switches and English, German and Turkish descriptions.
Validate them locally with:

```powershell
winget validate --manifest .\winget\manifests\t\Tutualican\GuvenOn\0.2.0
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
- The service runs as `LocalSystem` and is the only component that owns WFP
  filters and DNS policy. IPC commands are enumerated, never free-form.
- The Windows control pipe currently accepts any authenticated local user, so
  another signed-in user on a shared machine could toggle protection. Restricting
  it to the interactive user is future hardening.
- Windows filters use a dynamic WFP session, so a service crash removes the
  filters instead of locking the machine out. That means a crash fails open;
  persistent filters with a verified cleanup path are still a release blocker.
- DNS changes are snapshotted under `%ProgramData%\GuvenOn` and restored on
  disable, pause, clean uninstall and the next service start after a crash.
- Windows protection is session-scoped in 0.2.0. A reboot or service restart
  leaves protection off until the tray app enables it again.
- The Linux daemon runs as root, restricts its control socket to root, and
  leaves the nftables table in place if the daemon crashes. Package removal
  runs `--cleanup` before files are deleted.
- VPN and other virtual adapters are not modeled yet. VPN-internal DNS or LAN
  access can be blocked while protection is on, so test before relying on it.
- On Linux without systemd-resolved, the `/etc/resolv.conf` fallback can be
  overwritten by NetworkManager or another resolver manager.
- No per-query DNS logging. The UI is driven by counts and state changes.
- Preview installers are unsigned until the code-signing account is active.
  Publish SHA-256 hashes and expect SmartScreen warnings in the meantime.

## Roadmap

The detailed, research-backed backlog lives in [ROADMAP.md](ROADMAP.md).

1. Finish the Windows guard: trust-aware profiles, LAN allowlist, automatic
   captive-portal detection, DoH handling, full IPv6 isolation, persistent
   fail-closed filters with a verified panic cleanup, and self-test.
2. Linux follow-up: PolicyKit/D-Bus, NetworkManager integration, arm64 packages
   and a small tray UI.
3. WireGuard, split tunneling, per-app rules, visibility and threat alerts.
4. Signed update manifest, silent installer handoff, rollback.
5. WinGet community submission for the 0.2.0 manifests.
6. Code signing and SmartScreen reputation.

Linux packaging starts with `.deb`; Flatpak/AppImage are not a substitute for a
system-wide firewall/DNS daemon.

## License

Apache License 2.0. See [LICENSE](LICENSE).

Guven-ON is an independent project and is not affiliated with Microsoft.
