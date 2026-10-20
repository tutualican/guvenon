# Guven-ON — Advanced Product Roadmap

Status: living document · Updated: 2026-10-20

This document turns the research into a concrete, prioritized backlog. It covers
the Windows product, the new Linux target (`.deb` first), and the shared
protection model. It is deliberately broader than the current preview; items are
not shipped until their acceptance tests pass.

## 1. Product thesis

Guven-ON is **not another VPN** and not another generic firewall front-end.
It is a **trust-aware network guard** for laptops that join networks you do not
control.

The core promise:

> While you are on an untrusted network, Guven-ON blocks the leaks and local
> exposure you did not choose, keeps the network usable, and proves that it is
> working. On trusted networks it gets out of the way.

That means three things must be true before a release may call itself
"protection":

1. **DNS cannot silently escape** to the hotspot, router, or a rogue DHCP server.
2. **Local-network exposure is closed** (SMB, NetBIOS, mDNS, SSDP, UPnP,
   discovery, inbound connections) unless the user allowlists a device.
3. **The failure mode is safe**: a crash, reboot, sleep/wake, or network switch
   must not quietly restore unprotected traffic on an untrusted profile.

The preview releases so far built the delivery vehicle: tray app, service,
installer, updates, localization, and site. The first opt-in protection engine
now exists on both platforms: persistent WFP filters plus adapter DNS on Windows,
and an nftables table plus systemd-resolved DNS on Linux. It is deliberately
manual and still missing the P0 items below: trust profiles, a LAN allowlist,
automatic captive-portal detection, DoH handling, full IPv6 isolation, and a
physical leak matrix for sleep/wake and captive portals. The persistent-filter
crash path is now exercised by an elevated Windows CI job.

## 2. What users consistently ask for

The recurring signals from comparable tools, vendor documentation, and user
discussions point to the same priorities.

| Rank | User need | What it means for Guven-ON |
| --- | --- | --- |
| 1 | **Proof, not a placebo** | A built-in leak/self-test with explicit pass/fail results. Never show a green shield that only means "service running". |
| 2 | **A kill switch that survives real life** | Must hold through sleep/wake, Wi-Fi changes, service crash, reboot, and VPN reconnect. Software-only "block if process exits" is not enough. |
| 3 | **Captive portals must not break** | Hotel/airport portals are the most common reason people disable a VPN or firewall and forget to re-enable it. Detect, open, authenticate, re-arm automatically. |
| 4 | **LAN must still work when intended** | Printers, NAS, Chromecast, development servers, and localhost. Default isolation, explicit allowlist, temporary unlock. |
| 5 | **No popup fatigue** | Every connection prompt trains users to click Allow. Use a small number of clear decisions and a learning/review flow, not per-packet dialogs. |
| 6 | **Automatic protection on unknown Wi-Fi** | Users forget to enable protection. Trust prompts and per-network profiles are more valuable than another dashboard. |
| 7 | **DNS privacy without homework** | DoH/DoT, sane resolver presets, custom resolver, IPv6 coverage, and no per-query logging. |
| 8 | **Visibility and explainability** | "What is blocked and why" in plain language. Alerts for DNS change, gateway change, duplicate SSID, ARP spoofing, and rogue DHCP. |
| 9 | **Local-first and no account** | No telemetry, no browsing history upload, no subscription required for the core guard. |
| 10 | **Cross-platform parity and packaging** | Windows installer and Linux `.deb` that install, upgrade, and uninstall cleanly, with native tray/dock integration. |

Additional signals:

- Firewall users value **whitelist modes, LAN-only rules, temporary rules,
  learning mode, IPv6, WSL/container support, and tamper protection**; they
  dislike steep rule editors and noisy prompts (TinyWall, simplewall).
- Network-monitor users value **new-device alerts, evil-twin detection, ARP
  spoofing alerts, DNS-change alerts, and a simple timeline** (GlassWire).
- Privacy users value **secure DNS, per-app control, local history, filter
  lists, and transparent "no logs" behavior** (Portmaster, NextDNS, Quad9).
- Linux users expect **native packages**; `.deb` remains the preferred format
  for Debian/Ubuntu users, while Flatpak is a poor fit for a system-wide
  firewall/DNS daemon because the privileged part must live outside the sandbox.
- Linux integration must respect **nftables, systemd-resolved, NetworkManager,
  PolicyKit, and desktop tray conventions** rather than replacing them.

## 3. Competitive gap

| Product class | Strength | Gap Guven-ON can fill |
| --- | --- | --- |
| Windows Firewall | Built in, stable, invisible | Not trust-aware; advanced UI is intimidating; no DNS-leak story; no per-network protection narrative. |
| simplewall | Real WFP filtering, tiny, per-app rules, IPv6/WSL | Power-user tool; Windows-only; no trust profiles, captive-portal flow, or DNS/LAN guard by default. |
| TinyWall | No popups, whitelist, LAN-only rules, no kernel driver | Windows-only; not focused on public-Wi-Fi DNS leaks or proof/testing; configuration still assumes a technical user. |
| GlassWire | Excellent visibility, alerts, evil-twin/ARP/DNS-change monitoring | Monitor-first; firewall is secondary; paid tiers; not a fail-closed traveler tool. |
| Portmaster | Secure DNS, per-app control, filter lists, cross-platform | Kernel driver, complex privacy-suite surface, freemium; not a one-click public-Wi-Fi guard. |
| VPN clients | Encryption, kill switch, captive-portal workarounds | Subscription and exit-node model; often break LAN/captive portals; kill switch conflicts with split tunneling; no LAN isolation. |
| DNS filters (NextDNS, AdGuard, Quad9) | Fast, simple DNS protection | No OS-level LAN isolation, no trust-aware fail-closed policy, no self-test of the whole path. |

The open position is:

**trust-aware public-Wi-Fi protection + verifiable DNS/LAN leak prevention +
one-click undo + Windows/Linux parity, without an account or a custom kernel
driver.**

## 4. Product principles

1. **Honest states.** Monitor-only, protected, degraded, and failed are
   different states. Never reuse a green icon for all of them.
2. **Fail closed only where the user asked.** Untrusted profile: default deny.
   Trusted profile: no filters. Unknown profile: ask, with a safe default.
3. **Keep the network usable.** DHCP, ARP/NDP, loopback, the captive portal, and
   explicitly allowlisted LAN devices must always work.
4. **One-click escape.** Panic/reset must restore normal networking in seconds,
   without reinstalling or editing config files.
5. **No custom kernel driver.** Windows Filtering Platform on Windows,
   nftables on Linux. No driver to sign, crash, or maintain.
6. **Local-first.** No account, no telemetry, no DNS query history. Logs are
   local, bounded, and redacted by default.
7. **Explain, do not nag.** A small number of high-value decisions; no
   allow/deny popup for every connection.
8. **Uninstall means uninstall.** Filters, DNS settings, service, autostart,
   and rules must be removed or restored, including after a crash.
9. **Test the leaks, not the happy path.** Every release must pass a leak matrix
   on real Windows and Linux VMs.
10. **Native packaging.** `.exe` installer on Windows, `.deb` on
    Debian/Ubuntu, with signed metadata and clean upgrades.

## 5. Advanced feature backlog

Priority: **P0** = required for "real protection" release; **P1** = next major
release; **P2** = later; **P3** = explore. Effort/risk are rough.

### A. Protection core

| ID | Feature | Priority | Platform | Effort | Risk | Notes / acceptance |
| --- | --- | --- | --- | --- | --- | --- |
| A1 | Trust-aware network profiles | P0 | Win/Linux | L | M | Trust by SSID + BSSID + security + gateway; first-seen prompt; per-network rules; auto "untrusted" for open networks. |
| A2 | DNS leak guard | P0 | Win/Linux | L | H | Allow DNS only to the selected resolver; block port 53/853/DoH endpoints outside policy; IPv4 + IPv6; per-interface; verify with test. |
| A3 | Encrypted DNS (DoH/DoT) | P0 | Win/Linux | M | M | Presets (Cloudflare, Quad9, NextDNS, custom); Windows NRPT/DoH; Linux systemd-resolved DoT or local stub; no silent downgrade. |
| A4 | LAN exposure guard | P0 | Win/Linux | L | H | Block RFC1918/ULA/link-local/multicast discovery on untrusted; allow DHCP/ARP/NDP; block inbound; optional SMB/NetBIOS/mDNS/SSDP/UPnP rules. |
| A5 | LAN allowlist | P0 | Win/Linux | M | M | Device/port/CIDR allowlist; printer/NAS/Chromecast wizard; temporary access timer; visible active exceptions. |
| A6 | IPv6 leak guard | P0 | Win/Linux | M | H | Prevent AAAA/DNS and direct IPv6 bypass; keep NDP/DHCPv6 working; test IPv6-only and dual-stack networks. |
| A7 | Fail-closed kill switch | P0 | Win/Linux | L | H | Only on untrusted; survive crash/reboot/sleep/wake; allow DHCP/portal/VPN endpoint; explicit blocked state; auto-recover after tunnel/network returns. |
| A8 | Captive-portal mode | P0 | Win/Linux | L | H | Detect via OS + probe; temporarily allow portal HTTP/HTTPS/DNS; open browser; re-arm automatically; hard timeout; never leave open. |
| A9 | Panic / reset | P0 | Win/Linux | S | L | Tray + CLI + optional global shortcut; remove filters, restore DNS, flush state, show what changed. |
| A10 | Protection self-test | P0 | Win/Linux | M | M | Test DNS resolver, DNS leak, IPv6, LAN block, internet reachability, kill switch, portal path; show pass/fail and evidence. |
| A11 | Boot-time / crash safety | P0 | Win/Linux | M | H | Watchdog; boot-time rules or service-before-network; filter ownership; cleanup on uninstall; no stale filters after crash. |
| A12 | Coexistence guard | P0 | Win/Linux | L | H | Detect Windows VPN/WFP/Hyper-V/WSL/Docker and Linux firewalld/ufw/Docker/libvirt; never flush foreign rules; warn on conflicts. |
| A13 | WireGuard tunnel | P1 | Win/Linux | XL | H | Import config; endpoint allowlist; kill-switch integration; DNS through tunnel; split tunneling later. |
| A14 | Split tunneling | P2 | Win/Linux | L | H | Per-app or per-CIDR; must explicitly warn that kill switch has priority; test DNS leaks. |
| A15 | Tamper resistance | P1 | Win/Linux | M | M | Service ACL, signed config, audit of rule changes; no silent disable by unprivileged process. |

### B. Usability and UX

| ID | Feature | Priority | Platform | Effort | Risk | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| B1 | Three clear modes | P0 | Win/Linux | S | L | Home/trusted, Public/untrusted, Lockdown. Paused is a timed exception, not a fourth permanent mode. |
| B2 | New-network trust prompt | P0 | Win/Linux | M | M | Show SSID, security, gateway, DNS, first seen; safe default for open networks; remember choice. |
| B3 | Explainable status | P0 | Win/Linux | M | L | "Internet on · DNS protected · LAN blocked · IPv6 blocked"; plain language; no raw rule IDs. |
| B4 | Allowlist wizard | P0 | Win/Linux | M | M | Discover printers/NAS/media devices; add by device; test access; one-click revoke. |
| B5 | No popup fatigue | P0 | Win/Linux | S | L | Batch notifications; optional "ask to connect" off by default; review queue instead of per-connection prompts. |
| B6 | Tray/dock integration | P0 | Win/Linux | M | L | Windows NotifyIcon; Linux AppIndicator/StatusNotifier; start minimized; quick actions. |
| B7 | Onboarding + proof | P0 | Win/Linux | M | L | Three steps: choose mode, run self-test, show result. Undo always visible. |
| B8 | Localization + accessibility | P1 | Win/Linux | M | L | EN/DE/TR, keyboard navigation, screen readers, contrast, reduced motion, high-DPI. |
| B9 | Light/dark/high-contrast themes | P1 | Win/Linux | M | L | Keep the cream/navy identity; respect OS theme; no low-contrast gold text. |
| B10 | CLI | P1 | Win/Linux | M | L | `guvenonctl status`, `trust`, `untrust`, `panic`, `test`, `logs`, `export`; English, JSON output. |
| B11 | Redacted diagnostics export | P1 | Win/Linux | S | L | Version, OS, interface state, rule summary, test results; no SSIDs? optional hash; no DNS queries. |
| B12 | Temporary unlock | P0 | Win/Linux | S | M | "Allow LAN for 15 min" / "Pause protection for 10 min"; countdown; automatic re-arm; audit entry. |

### C. Visibility and diagnostics

| ID | Feature | Priority | Platform | Effort | Risk | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| C1 | Blocked/connection events | P1 | Win/Linux | M | M | Time, app, direction, remote IP/port, rule, count. No packet payloads. |
| C2 | DNS status + leak test | P0 | Win/Linux | M | M | Actual resolver(s), encrypted/unencrypted, IPv4/IPv6, test result. |
| C3 | Network details | P0 | Win/Linux | S | L | SSID/BSSID, security, gateway, DNS, IPv6, portal state, VPN state. |
| C4 | Threat alerts | P1 | Win/Linux | L | H | Duplicate SSID/evil twin, gateway change, DNS change, ARP spoof, rogue DHCP. Avoid alert storms; require confidence thresholds. |
| C5 | Local history | P2 | Win/Linux | M | M | 24h/7d retention, clear on demand, off by default for per-connection detail. |
| C6 | Filter counters | P1 | Win/Linux | S | L | Counts by category (DNS, LAN, IPv6, inbound), no content. |
| C7 | Per-app bandwidth/usage | P3 | Win/Linux | L | M | Optional; privacy-sensitive; not needed for the core promise. |

### D. Advanced / power user

| ID | Feature | Priority | Platform | Effort | Risk | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| D1 | Per-app rules | P2 | Win/Linux | L | H | Allow/block/LAN-only; templates; no popup default; import/export. |
| D2 | Custom DNS + filter lists | P2 | Win/Linux | M | M | Malware/phishing lists optional; ads/trackers off by default to avoid breakage; allowlist. |
| D3 | Profiles + import/export | P1 | Win/Linux | M | L | Per-network profiles; JSON schema versioned; validation; no secrets in plain text. |
| D4 | Rule dry-run/simulator | P1 | Win/Linux | M | L | Show which rule would match a test destination before applying. |
| D5 | WSL/container awareness | P2 | Win/Linux | L | M | Windows WSL/Hyper-V/Docker; Linux namespaces/containers; don't break localhost/dev traffic. |
| D6 | VPN coexistence mode | P1 | Win/Linux | M | H | Detect third-party VPN; avoid double kill switch; explicit ownership of DNS/routes. |
| D7 | Managed configuration | P3 | Win/Linux | L | M | Signed local policy file for advanced users; no cloud account required. |

### E. Linux platform (.deb first)

| ID | Feature | Priority | Platform | Effort | Risk | Notes / acceptance |
| --- | --- | --- | --- | --- | --- | --- |
| E1 | Debian package | P0 | Linux | M | M | `guvenon_<version>_amd64.deb` and `arm64`; `dpkg -i` works; dependencies declared; `lintian` clean enough for a third-party repo. |
| E2 | systemd service | P0 | Linux | S | M | `guvenon.service`, root daemon, `After=network-pre.target`, restart policy, sandboxing; no filters left after stop/uninstall. |
| E3 | PolicyKit + D-Bus IPC | P0 | Linux | M | M | GUI runs unprivileged; actions require polkit auth; enumerated commands only; no free-form shell. |
| E4 | nftables backend | P0 | Linux | L | H | Own `inet guvenon` table; atomic apply/rollback; sets for DNS/allowlist; never `flush ruleset`; coexist with Docker/firewalld/ufw. |
| E5 | systemd-resolved integration | P0 | Linux | L | H | DoT/DoH strategy; per-link DNS; preserve split DNS and VPN; restore original config on stop. |
| E6 | NetworkManager integration | P0 | Linux | M | M | Detect SSID/BSSID/security, connectivity/portal state; react to network changes; fallback for systemd-networkd/iwd. |
| E7 | Desktop integration | P1 | Linux | M | L | `.desktop`, icons, AppIndicator, notifications, autostart, dark/light theme. |
| E8 | .deb repository + signing | P1 | Linux | M | M | `apt` repo with signed `InRelease`; upgrade path; rollback; checksums. |
| E9 | Linux self-test lab | P0 | Linux | L | H | Network namespaces + VMs; IPv4/IPv6, portal, WireGuard, Docker/firewalld coexistence, uninstall cleanup. |
| E10 | Fedora/RHEL RPM | P2 | Linux | M | M | After `.deb` is stable; same daemon, RPM packaging and SELinux policy. |
| E11 | Flatpak/AppImage | P3 | Linux | M | M | UI-only packaging is possible, but system daemon/firewall needs host integration; do not pretend a sandbox is enough. |

### F. Cross-platform architecture

| ID | Item | Priority | Notes |
| --- | --- | --- | --- |
| F1 | Shared core library | P0 | Settings, trust model, policy compiler, update manifest, diagnostics, self-test definitions. Keep platform code thin. |
| F2 | Windows backend | P0 | WFP filters, WLAN API/NLM, NRPT/DoH, service, named pipe. |
| F3 | Linux backend | P0 | nftables, systemd-resolved, NetworkManager D-Bus, systemd, polkit. |
| F4 | UI strategy | P0 decision | Keep WPF for Windows and add an Avalonia Linux shell first; evaluate a full Avalonia migration later. Avoid a rushed rewrite of the polished Windows UI. |
| F5 | IPC schema | P0 | One enum-based command schema over named pipes (Windows) and D-Bus (Linux); no arbitrary commands. |
| F6 | Update pipeline | P0 | GitHub release manifest without API; Windows installer + `.deb` assets; signed hashes; staged rollout. |
| F7 | CI matrix | P0 | `windows-latest` + `ubuntu-24.04`; build, unit tests, `.deb` lint, install/uninstall smoke tests; privileged nftables tests in disposable VM/runner. |
| F8 | Signing | P1 | Authenticode for Windows; GPG-signed apt repo for Linux; published SHA-256 for every asset. |

## 6. Safety invariants

These are release blockers, not suggestions.

1. On a **trusted** profile, no Guven-ON filters are active unless the user
   explicitly enables lockdown.
2. On an **untrusted** profile, DNS, LAN, and IPv6 policy is fail-closed; the
   only exceptions are DHCP, ARP/NDP, loopback, the captive portal, the selected
   resolver, and explicit allowlist entries.
3. A service crash, reboot, sleep/wake, or network change never silently
   restores direct DNS or LAN access on an untrusted profile.
4. Captive-portal mode is time-bounded and automatically re-arms. It cannot be
   left open by closing the browser or killing the UI.
5. Panic/reset removes filters and restores DNS within seconds, and reports what
   was restored.
6. Uninstall removes service, filters, DNS changes, autostart, and rules. A
   leftover filter that breaks networking is a critical bug.
7. Updates are atomic: on failure, the previous working version and policy are
   restored.
8. No DNS query logging, no packet payload capture, no telemetry by default.
9. Foreign firewall/VPN rules are never flushed or overwritten.
10. The self-test must pass on a clean VM before a build is called "protected".

## 7. Release plan

### 0.2 – Opt-in protection preview (shipped)

**Shipped in 0.2.0**
- Windows: dynamic WFP DNS/LAN guard, trusted adapter DNS, manual pause and
  panic reset, tray/service/installer/update path, English, German and Turkish.
- Linux alpha: nftables DNS/LAN guard, systemd-resolved and `/etc/resolv.conf`
  handling, root-only control socket, CLI and `.deb` packaging.
- Shared policy compiler, unit tests and CI nftables syntax validation.

**On main after 0.2.0 (next preview build)**
- Persistent WFP provider, sublayer and filters with deterministic keys.
- Service-state restore and transactional cleanup after a crash, plus an
  elevated `--wfp-cleanup` recovery switch and uninstall cleanup.

**Still required before "real protection"**
- Trust profiles and a new-network prompt.
- LAN allowlist and automatic captive-portal detection.
- IPv6 isolation and DoH handling.
- A documented leak matrix on Windows 11 and Ubuntu/Debian VMs, including
  portal, IPv6, sleep/wake, service crash, reboot and uninstall.

### 0.3 — Cross-platform + VPN

- WireGuard import and tunnel-aware kill switch.
- Split tunneling with explicit conflict warnings.
- Per-network profiles and import/export.
- Linux tray UI, `.deb` apt repository, arm64.
- CLI parity and redacted diagnostics.
- Threat alerts v1 (DNS/gateway change, duplicate SSID).

### 0.4 — Visibility + advanced control

- Per-app rules and LAN-only rules.
- Optional malware/phishing DNS lists; ads/trackers off by default.
- Local history and better explainability.
- WSL/container and third-party VPN coexistence hardening.
- Fedora/RHEL RPM.

### 1.0 — Stable

- Signed Windows builds and signed apt repository.
- Independent security review and reproducible-build documentation.
- Stable config schema and migration policy.
- Support matrix, recovery guide, and enterprise/local policy options.

## 8. Scoring snapshot

A quick way to choose the next sprint. Scores are relative, not absolute.

| Feature | User value | Differentiation | Effort | Risk | Do next? |
| --- | --- | --- | --- | --- | --- |
| DNS leak guard | Very high | High | L | H | Yes |
| LAN exposure guard | Very high | High | L | H | Yes |
| Captive-portal mode | Very high | High | L | H | Yes |
| Self-test / leak proof | Very high | Very high | M | M | Yes |
| Trust profiles | Very high | High | L | M | Yes |
| Kill switch | High | Medium | L | H | Yes |
| Allowlist wizard | High | High | M | M | Yes |
| Linux `.deb` alpha | High | High | M | M | Yes |
| WireGuard | Medium | Medium | XL | H | After P0 |
| Per-app rules | Medium | Low | L | H | Later |
| Threat alerts | Medium | Medium | L | H | After P0 |
| Filter lists | Medium | Low | M | M | Optional |
| History/bandwidth | Low | Low | L | M | Later |

## 9. Non-goals

- A VPN subscription, exit nodes, or "unblock Netflix" marketing.
- A custom kernel driver.
- Silent telemetry, account creation, or cloud DNS history.
- Per-connection popups that train users to click Allow.
- Blocking ads/trackers by default before false-positive handling exists.
- Always-on lockdown on trusted home networks by default.
- Claiming protection when only the service is running.
- Shipping Linux as a second-class port; `.deb` must have the same safety
  invariants as Windows.

## 10. Open decisions

1. **UI stack:** WPF + Avalonia Linux shell first, or full Avalonia migration?
   Recommendation: keep WPF for Windows in 0.2, add Avalonia on Linux, revisit
   after parity.
2. **Default resolver:** Cloudflare, Quad9, or "custom only"? Recommendation:
   Quad9/Cloudflare presets plus custom, with a visible choice during onboarding.
3. **DoH vs DoT:** Windows NRPT/DoH and Linux systemd-resolved DoT may differ.
   Document per-platform behavior instead of pretending it is identical.
4. **WireGuard:** built-in client vs "bring your own config". Recommendation:
   import existing WireGuard config first; no account/server.
5. **Default on unknown networks:** auto-protect or ask? Recommendation:
   auto-protect open networks, ask for secured unknown networks, always allow
   one-click undo.
6. **Open source:** confirm license and whether Linux packaging lives in the
   same repository.
7. **Linux support matrix:** Debian 12/13, Ubuntu 24.04/26.04, Linux Mint,
   Pop!_OS first; Fedora/RHEL later.
8. **Windows 10:** keep Windows 11 22H2+ for the protection phase; revisit a
   Windows 10 DNS path only if it can be tested safely.

## 11. Research sources

Primary/technical:

- MITRE ATT&CK — Evil Twin, DHCP Spoofing, ARP Cache Poisoning:
  https://attack.mitre.org/techniques/T1557/004/
  https://attack.mitre.org/techniques/T1557/003/
  https://attack.mitre.org/techniques/T1557/002/
- Microsoft — Zero Trust DNS / WFP integration:
  https://learn.microsoft.com/en-us/windows/security/operating-system-security/network-security/zero-trust-dns
- systemd-resolved — DoT and DNS configuration:
  https://www.freedesktop.org/software/systemd/man/latest/resolved.conf.html
- NetworkManager — connectivity/portal states and dispatcher:
  https://people.freedesktop.org/~lkundrak/nm-docs/nmcli.html
  https://wiki.archlinux.org/title/NetworkManager
- Debian — binary packages and systemd packaging:
  https://www.debian.org/doc/debian-policy/ch-binary.html
  https://wiki.debian.org/Teams/pkg-systemd/Packaging
- nftables — ruleset, hooks, sets:
  https://wiki.nftables.org/
  https://manpages.debian.org/bookworm/nftables/nft.8.en.html

Comparable products:

- TinyWall features:
  https://tinywall.pados.hu/features.php
- simplewall:
  https://github.com/henrypp/simplewall
- GlassWire features and alert list:
  https://www.glasswire.com/features/
  https://www.glasswire.com/userguide
- Portmaster:
  https://github.com/safing/portmaster
- OpenSnitch:
  https://github.com/evilsocket/opensnitch
- Quad9:
  https://www.quad9.net/
- NextDNS / AdGuard DNS:
  https://nextdns.io/
  https://adguard-dns.io/

User-facing pain points and vendor behavior:

- Mozilla VPN — captive portals require temporarily disabling the VPN:
  https://support.mozilla.org/en-US/kb/no-internet-while-using-mozilla-vpn
- ExpressVPN — split-tunneling DNS leak:
  https://www.pcmag.com/news/expressvpn-removes-split-tunneling-feature-for-leaking-dns-requests
- ExpressVPN — DNS leak test:
  https://www.expressvpn.com/dns-leak-test
- RTINGS / user discussion — kill switches can leak on reboot/reconnect:
  https://www.reddit.com/r/hardware/comments/1mblovm/your_vpn_kill_switch_wont_always_stop_all_leaks
- NordVPN — kill switch vs split tunneling conflict:
  https://support.nordvpn.com/hc/en-us/articles/46236661588497-How-Kill-Switch-and-Split-Tunneling-interact-in-NordVPN
- Linux packaging preference discussion:
  https://forums.linuxmint.com/viewtopic.php?t=406191

## 12. Immediate next steps

1. Write the platform-neutral **trust/profile and policy model** (no OS calls),
   with unit tests for DNS/LAN/IPv6/portal decisions.
2. Build a **policy compiler + dry-run output** for both WFP and nftables before
   applying any real filter.
3. Stand up a **lab**: Windows 11 VM, Ubuntu/Debian VM, an OpenWrt AP with a
   captive portal, and a WireGuard endpoint.
4. Implement **DNS guard** first, then **LAN guard**, then **kill switch**, then
   **captive-portal mode**, each with a self-test.
5. Add `.deb` packaging and systemd/polkit early, so Linux integration problems
   surface before the feature set grows.
6. Do not label a build "protected" until the leak matrix passes and the
   self-test can prove it.

0.2.0 is no longer monitor-only: it applies real DNS and LAN rules. The 0.2.0
artifact still fails open after a Windows service crash; main now uses persistent
WFP filters and state restore. The elevated Windows CI job verifies enable, pause
cleanup, a forced crash with filters still installed, recovery, disable and
uninstall cleanup. A physical Windows 11 sleep/wake and captive-portal leak matrix
is still pending. Profiles, an allowlist, DoH coverage and full IPv6 isolation
remain missing, so the product must stay labeled an opt-in preview until that
physical matrix passes.
