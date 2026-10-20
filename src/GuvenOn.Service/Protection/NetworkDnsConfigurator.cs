using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using GuvenOn.Core.Protection;
using Microsoft.Extensions.Logging;

namespace GuvenOn.Service.Protection;

/// <summary>
/// Points active physical adapters at the trusted DNS set and restores the
/// previous configuration when protection is turned off. Without this step the
/// WFP DNS guard would block the DHCP-provided resolver and name resolution
/// would simply fail.
/// </summary>
internal sealed class NetworkDnsConfigurator : IDisposable
{
    private readonly ILogger _logger;
    private readonly List<AdapterSnapshot> _snapshots = new();
    private bool _applied;

    public NetworkDnsConfigurator(ILogger logger)
    {
        _logger = logger;
    }

    private static string SnapshotPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GuvenOn",
        "dns-snapshot.json");

    public int Apply(ProtectionPolicy policy)
    {
        if (_applied)
        {
            return _snapshots.Count;
        }

        // Never capture the trusted DNS values as the new "original" when a
        // previous snapshot could not be restored. Retry the restore first and
        // fail closed if it is still incomplete.
        if (File.Exists(SnapshotPath))
        {
            var restored = RestorePersisted(_logger, out var snapshotFound);
            if (snapshotFound && !restored)
            {
                throw new InvalidOperationException(
                    "A previous DNS snapshot could not be restored. Reconnect the adapter or reboot before enabling protection.");
            }
        }

        var v4Servers = policy.DnsServersV4.Select(address => address.ToString()).ToArray();
        var v6Servers = policy.DnsServersV6.Select(address => address.ToString()).ToArray();

        foreach (var adapter in GetActivePhysicalAdapters())
        {
            var properties = adapter.GetIPProperties();
            _snapshots.Add(new AdapterSnapshot(
                adapter.Name,
                IsDhcpEnabled(properties),
                properties.DnsAddresses
                    .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(address => address.ToString())
                    .ToArray(),
                properties.DnsAddresses
                    .Where(address => address.AddressFamily == AddressFamily.InterNetworkV6)
                    .Select(address => address.ToString())
                    .ToArray()));
        }

        if (_snapshots.Count == 0)
        {
            throw new InvalidOperationException(
                "No active physical network adapter was found; refusing to enable DNS blocking without a trusted resolver target.");
        }

        // Persist before changing any adapter. If the write fails, no DNS state
        // has been touched and there is nothing to restore or delete.
        PersistSnapshot();
        _applied = true;

        try
        {
            foreach (var snapshot in _snapshots)
            {
                if (v4Servers.Length > 0)
                {
                    SetDns(snapshot.AdapterName, "ipv4", v4Servers);
                }

                if (v6Servers.Length > 0)
                {
                    try
                    {
                        SetDns(snapshot.AdapterName, "ipv6", v6Servers);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(
                            exception,
                            "Could not apply IPv6 DNS to adapter {Adapter}; IPv4 DNS remains active.",
                            snapshot.AdapterName);
                    }
                }

                _logger.LogInformation(
                    "Trusted DNS applied to adapter {Adapter} (DHCP was {Dhcp}).",
                    snapshot.AdapterName,
                    snapshot.WasDhcp);
            }

            FlushDnsCache();
            return _snapshots.Count;
        }
        catch
        {
            Restore();
            throw;
        }
    }

    private static IEnumerable<NetworkInterface> GetActivePhysicalAdapters()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter =>
                adapter.OperationalStatus == OperationalStatus.Up &&
                adapter.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet &&
                adapter.GetIPProperties().GatewayAddresses.Any(gateway =>
                    !gateway.Address.Equals(IPAddress.Any) &&
                    !gateway.Address.Equals(IPAddress.IPv6Any)));
    }

    private static bool IsDhcpEnabled(IPInterfaceProperties properties)
    {
        try
        {
            return properties.GetIPv4Properties()?.IsDhcpEnabled == true;
        }
        catch
        {
            return false;
        }
    }

    private void SetDns(string adapterName, string family, IReadOnlyList<string> servers)
    {
        if (servers.Count == 0)
        {
            RunNetsh(
                "interface",
                family,
                "set",
                "dns",
                $"name={adapterName}",
                "source=dhcp");
            return;
        }

        RunNetsh(
            "interface",
            family,
            "set",
            "dns",
            $"name={adapterName}",
            "static",
            servers[0],
            "primary");

        for (var index = 1; index < servers.Count; index++)
        {
            RunNetsh(
                "interface",
                family,
                "add",
                "dns",
                $"name={adapterName}",
                servers[index],
                $"index={index + 1}");
        }
    }

    private static void RunNetsh(params string[] arguments)
    {
        var netsh = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "netsh.exe");

        var startInfo = new ProcessStartInfo(netsh)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start netsh.exe.");

        // Drain both pipes concurrently. Reading stdout to completion first can
        // deadlock when a failing netsh.exe fills the stderr pipe.
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(10_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort.
            }

            throw new TimeoutException("netsh.exe did not finish within 10 seconds.");
        }

        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"netsh.exe failed with exit code {process.ExitCode}: {error}{output}".Trim());
        }
    }

    private static void FlushDnsCache()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "ipconfig.exe"),
                "/flushdns")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            process?.WaitForExit(5_000);
        }
        catch
        {
            // A cache flush is helpful but not required for correctness.
        }
    }

    public bool Restore()
    {
        if (!_applied)
        {
            return true;
        }

        _applied = false;
        var allRestored = true;
        var presentAdapters = NetworkInterface.GetAllNetworkInterfaces()
            .Select(adapter => adapter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in _snapshots)
        {
            if (!presentAdapters.Contains(snapshot.AdapterName))
            {
                _logger.LogInformation(
                    "Adapter {Adapter} is no longer present; discarding its DNS snapshot.",
                    snapshot.AdapterName);
                continue;
            }

            try
            {
                RestoreFamily(snapshot.AdapterName, "ipv4", snapshot.WasDhcp, snapshot.IPv4Servers);
                RestoreFamily(
                    snapshot.AdapterName,
                    "ipv6",
                    wasDhcp: snapshot.IPv6Servers.Count == 0,
                    servers: snapshot.IPv6Servers);
            }
            catch (Exception exception)
            {
                allRestored = false;
                _logger.LogWarning(
                    exception,
                    "Could not restore DNS settings for adapter {Adapter}.",
                    snapshot.AdapterName);
            }
        }

        _snapshots.Clear();
        FlushDnsCache();

        if (allRestored)
        {
            TryDeleteSnapshot();
        }

        return allRestored;
    }

    private void PersistSnapshot()
    {
        var directory = Path.GetDirectoryName(SnapshotPath)
            ?? throw new InvalidOperationException("The DNS snapshot directory is unavailable.");

        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(_snapshots, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        });

        var temporaryPath = SnapshotPath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, SnapshotPath, overwrite: true);
    }

    public static bool RestorePersisted(ILogger logger, out bool snapshotFound)
    {
        snapshotFound = false;

        try
        {
            var path = SnapshotPath;
            if (!File.Exists(path))
            {
                return false;
            }

            snapshotFound = true;

            var snapshots = JsonSerializer.Deserialize<List<AdapterSnapshot>>(
                File.ReadAllText(path),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (snapshots is null || snapshots.Count == 0)
            {
                TryDeleteSnapshot(logger);
                snapshotFound = false;
                return false;
            }

            var configurator = new NetworkDnsConfigurator(logger);
            configurator._snapshots.AddRange(snapshots);
            configurator._applied = true;
            return configurator.Restore();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not restore the persisted DNS snapshot.");
            return false;
        }
    }

    private void TryDeleteSnapshot()
    {
        TryDeleteSnapshot(_logger);
    }

    private static void TryDeleteSnapshot(ILogger logger)
    {
        try
        {
            if (File.Exists(SnapshotPath))
            {
                File.Delete(SnapshotPath);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not delete the DNS snapshot file.");
        }
    }

    private void RestoreFamily(
        string adapterName,
        string family,
        bool wasDhcp,
        IReadOnlyList<string> servers)
    {
        if (wasDhcp)
        {
            RunNetsh(
                "interface",
                family,
                "set",
                "dns",
                $"name={adapterName}",
                "source=dhcp");
            return;
        }

        SetDns(adapterName, family, servers);
    }

    public void Dispose() => Restore();
}

internal sealed record AdapterSnapshot(
    string AdapterName,
    bool WasDhcp,
    IReadOnlyList<string> IPv4Servers,
    IReadOnlyList<string> IPv6Servers);
