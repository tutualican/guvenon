using System.Text;
using GuvenOn.Core.Protection;

namespace GuvenOn.Linux;

/// <summary>
/// Configures trusted DNS through systemd-resolved when available, otherwise
/// through /etc/resolv.conf. Every change is backed up under /var/lib/guvenon
/// and restored by disable, pause, uninstall, or daemon shutdown.
/// </summary>
internal sealed class LinuxDnsConfigurator
{
    private const string Marker = "# Managed by Guven-ON";

    private static readonly string SystemctlPath = ProcessRunner.FindExecutable(
        "/usr/bin/systemctl",
        "/bin/systemctl",
        "systemctl");

    private static readonly string ResolvectlPath = ProcessRunner.FindExecutable(
        "/usr/bin/resolvectl",
        "/bin/resolvectl",
        "resolvectl");

    private bool _applied;

    public string? Method { get; private set; }

    public async Task ApplyAsync(
        ProtectionPolicy policy,
        CancellationToken cancellationToken = default)
    {
        if (_applied)
        {
            return;
        }

        Directory.CreateDirectory(LinuxPaths.StateDirectory);

        var useResolved = Directory.Exists("/run/systemd/resolve") && File.Exists(ResolvectlPath);

        try
        {
            if (useResolved)
            {
                await ApplyResolvedAsync(policy, cancellationToken).ConfigureAwait(false);
                Method = "systemd-resolved";
            }
            else
            {
                ApplyResolvConf(policy);
                Method = "resolv.conf";
            }

            _applied = true;
        }
        catch
        {
            await RestoreAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task ApplyResolvedAsync(
        ProtectionPolicy policy,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(LinuxPaths.ResolvedDropIn)
            ?? throw new InvalidOperationException("The systemd-resolved drop-in directory is unavailable.");

        Directory.CreateDirectory(directory);

        if (File.Exists(LinuxPaths.ResolvedDropIn) &&
            !File.ReadAllText(LinuxPaths.ResolvedDropIn).Contains(Marker, StringComparison.Ordinal))
        {
            File.Copy(LinuxPaths.ResolvedDropIn, LinuxPaths.ResolvedDropInBackup, overwrite: true);
        }

        var content = new StringBuilder()
            .AppendLine(Marker)
            .AppendLine("[Resolve]")
            .Append("DNS=")
            .AppendLine(string.Join(' ', policy.DnsServers.Select(address => address.ToString())))
            .AppendLine("FallbackDNS=")
            .AppendLine("Domains=~.")
            .ToString();

        File.WriteAllText(LinuxPaths.ResolvedDropIn, content);

        var restart = await ProcessRunner.RunAsync(
            SystemctlPath,
            new[] { "restart", "systemd-resolved" },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (restart.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not restart systemd-resolved: {restart.StandardError.Trim()}{restart.StandardOutput.Trim()}");
        }
    }

    private static void ApplyResolvConf(ProtectionPolicy policy)
    {
        if (File.Exists(LinuxPaths.ResolvConf) &&
            !File.ReadAllText(LinuxPaths.ResolvConf).Contains(Marker, StringComparison.Ordinal))
        {
            File.Copy(LinuxPaths.ResolvConf, LinuxPaths.ResolvConfBackup, overwrite: true);
        }

        var content = new StringBuilder()
            .AppendLine(Marker)
            .AppendLine("# Restored automatically by guvenonctl disable or service shutdown.");

        foreach (var server in policy.DnsServers)
        {
            content.Append("nameserver ").AppendLine(server.ToString());
        }

        File.WriteAllText(LinuxPaths.ResolvConf, content.ToString());
    }

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        var resolvedDropInExists =
            File.Exists(LinuxPaths.ResolvedDropIn) &&
            File.ReadAllText(LinuxPaths.ResolvedDropIn).Contains(Marker, StringComparison.Ordinal);

        if (resolvedDropInExists)
        {
            File.Delete(LinuxPaths.ResolvedDropIn);

            if (File.Exists(LinuxPaths.ResolvedDropInBackup))
            {
                File.Copy(LinuxPaths.ResolvedDropInBackup, LinuxPaths.ResolvedDropIn, overwrite: true);
                File.Delete(LinuxPaths.ResolvedDropInBackup);
            }

            var restart = await ProcessRunner.RunAsync(
                SystemctlPath,
                new[] { "restart", "systemd-resolved" },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (restart.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Could not restart systemd-resolved after restoring DNS: {restart.StandardError.Trim()}{restart.StandardOutput.Trim()}");
            }
        }

        var resolvConfIsOurs =
            File.Exists(LinuxPaths.ResolvConf) &&
            File.ReadAllText(LinuxPaths.ResolvConf).Contains(Marker, StringComparison.Ordinal);

        if (resolvConfIsOurs)
        {
            if (File.Exists(LinuxPaths.ResolvConfBackup))
            {
                File.Copy(LinuxPaths.ResolvConfBackup, LinuxPaths.ResolvConf, overwrite: true);
                File.Delete(LinuxPaths.ResolvConfBackup);
            }
            else
            {
                // We created /etc/resolv.conf because none existed. Remove our
                // file rather than leave the marker and a protected resolver
                // behind after disable or uninstall.
                File.Delete(LinuxPaths.ResolvConf);
            }
        }

        _applied = false;
        Method = null;
    }

    public bool IsApplied => _applied;

    public bool IsConfigured =>
        (File.Exists(LinuxPaths.ResolvedDropIn) &&
         File.ReadAllText(LinuxPaths.ResolvedDropIn).Contains(Marker, StringComparison.Ordinal)) ||
        (File.Exists(LinuxPaths.ResolvConf) &&
         File.ReadAllText(LinuxPaths.ResolvConf).Contains(Marker, StringComparison.Ordinal));
}
