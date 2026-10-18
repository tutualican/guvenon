using System.Diagnostics;
using System.Security.Principal;
using GuvenOn.Core;

namespace GuvenOn.Service;

/// <summary>
/// Installs and removes the Windows service through sc.exe. This path is only
/// reached from the elevated installer or from an elevated command prompt;
/// the tray app never calls it.
/// </summary>
internal static class ServiceInstaller
{
    private const int ServiceDoesNotExist = 1060;

    public static int Install()
    {
        if (!IsAdministrator())
        {
            Console.Error.WriteLine("Administrator rights are required to install the service.");
            return 5;
        }

        var executable = Environment.ProcessPath ??
                         Path.Combine(AppContext.BaseDirectory, "guvenonsvc.exe");

        if (!File.Exists(executable))
        {
            Console.Error.WriteLine($"Service executable not found: {executable}");
            return 2;
        }

        if (ServiceExists())
        {
            // Upgrade path: stop, then rewrite the binary path and start mode.
            Run("sc.exe", $"stop {AppInfo.ServiceName}");
            Thread.Sleep(1500);

            var configured = Run(
                "sc.exe",
                $"config {AppInfo.ServiceName} binPath= \"{executable}\" start= auto DisplayName= \"{AppInfo.ServiceDisplayName}\"");

            if (configured != 0)
            {
                return configured;
            }
        }
        else
        {
            var created = Run(
                "sc.exe",
                $"create {AppInfo.ServiceName} binPath= \"{executable}\" start= auto DisplayName= \"{AppInfo.ServiceDisplayName}\"");

            if (created != 0)
            {
                return created;
            }
        }

        Run("sc.exe", $"description {AppInfo.ServiceName} \"{AppInfo.ServiceDescription}\"");
        Run("sc.exe", $"failure {AppInfo.ServiceName} reset= 86400 actions= restart/5000/restart/10000/restart/30000");

        var started = Run("sc.exe", $"start {AppInfo.ServiceName}");

        // 1056 means the service is already running, which is fine for upgrades.
        return started == 1056 ? 0 : started;
    }

    public static int Uninstall()
    {
        if (!IsAdministrator())
        {
            Console.Error.WriteLine("Administrator rights are required to remove the service.");
            return 5;
        }

        Run("sc.exe", $"stop {AppInfo.ServiceName}");
        Thread.Sleep(1000);
        var deleted = Run("sc.exe", $"delete {AppInfo.ServiceName}");
        return deleted == ServiceDoesNotExist ? 0 : deleted;
    }

    private static bool ServiceExists() => Run("sc.exe", $"query {AppInfo.ServiceName}") == 0;

    private static int Run(string fileName, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return -1;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(output))
            {
                Console.WriteLine(output.Trim());
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.Error.WriteLine(error.Trim());
            }

            return process.ExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return -1;
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
