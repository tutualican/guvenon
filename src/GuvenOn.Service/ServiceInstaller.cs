using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
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
    private const int ServiceAlreadyRunning = 1056;
    private const int ServiceMarkedForDeletion = 1072;
    private const int ServiceNotActive = 1062;
    private const int ServiceDidNotRespond = 1053;

    private static readonly string ScExe = Path.Combine(Environment.SystemDirectory, "sc.exe");
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(20);

    public static int Install()
    {
        if (!IsAdministrator())
        {
            Console.Error.WriteLine("Administrator rights are required to install the service.");
            return 5;
        }

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) ||
            string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            // Running through `dotnet guvenonsvc.dll --install` must not register
            // dotnet.exe as the service binary.
            executable = Path.Combine(AppContext.BaseDirectory, "guvenonsvc.exe");
        }

        if (!File.Exists(executable))
        {
            Console.Error.WriteLine($"Service executable not found: {executable}");
            return 2;
        }

        if (ServiceExists())
        {
            // Upgrade path: stop and wait before rewriting the binary path.
            var stopped = StopAndWait(StopTimeout);
            if (stopped != 0)
            {
                return stopped;
            }

            var configured = Run(
                $"config {AppInfo.ServiceName} binPath= \"{executable}\" start= auto DisplayName= \"{AppInfo.ServiceDisplayName}\"");

            if (configured != 0)
            {
                return configured;
            }
        }
        else
        {
            var created = Run(
                $"create {AppInfo.ServiceName} binPath= \"{executable}\" start= auto DisplayName= \"{AppInfo.ServiceDisplayName}\"");

            if (created != 0)
            {
                return created;
            }
        }

        // Description and recovery are useful but not fatal for starting the service.
        Run($"description {AppInfo.ServiceName} \"{AppInfo.ServiceDescription}\"");
        Run($"failure {AppInfo.ServiceName} reset= 86400 actions= restart/5000/restart/10000/restart/30000");

        var started = Run($"start {AppInfo.ServiceName}");

        // 1056 means the service is already running, which is fine for upgrades.
        return started is 0 or ServiceAlreadyRunning ? 0 : started;
    }

    public static int Stop()
    {
        if (!IsAdministrator())
        {
            Console.Error.WriteLine("Administrator rights are required to stop the service.");
            return 5;
        }

        if (!ServiceExists())
        {
            return 0;
        }

        return StopAndWait(StopTimeout);
    }

    public static int Uninstall()
    {
        if (!IsAdministrator())
        {
            Console.Error.WriteLine("Administrator rights are required to remove the service.");
            return 5;
        }

        if (!ServiceExists())
        {
            return 0;
        }

        var stopped = StopAndWait(StopTimeout);
        if (stopped != 0)
        {
            return stopped;
        }

        // A service removed while one of its handles is still open is marked
        // for deletion. Retry briefly instead of failing the whole uninstall.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var deleted = Run($"delete {AppInfo.ServiceName}");
            if (deleted is 0 or ServiceDoesNotExist)
            {
                return 0;
            }

            if (deleted != ServiceMarkedForDeletion)
            {
                return deleted;
            }

            Thread.Sleep(500);
        }

        return ServiceMarkedForDeletion;
    }

    private static int StopAndWait(TimeSpan timeout)
    {
        try
        {
            using var controller = new ServiceController(AppInfo.ServiceName);
            controller.Refresh();

            if (controller.Status == ServiceControllerStatus.Stopped)
            {
                return 0;
            }

            if (controller.Status is ServiceControllerStatus.StartPending or ServiceControllerStatus.ContinuePending)
            {
                controller.WaitForStatus(ServiceControllerStatus.Running, timeout);
                controller.Refresh();
            }

            if (controller.Status is ServiceControllerStatus.StopPending or ServiceControllerStatus.PausePending)
            {
                controller.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                return 0;
            }

            if (controller.Status != ServiceControllerStatus.Stopped)
            {
                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            }

            return 0;
        }
        catch (InvalidOperationException)
        {
            // The service was removed between the existence check and this call.
            return 0;
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            Console.Error.WriteLine($"Service {AppInfo.ServiceName} did not stop within {timeout.TotalSeconds:0} seconds.");
            return ServiceDidNotRespond;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ServiceNotActive)
        {
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return -1;
        }
    }

    private static bool ServiceExists() => Run($"query {AppInfo.ServiceName}") == 0;

    private static int Run(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(ScExe, arguments)
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

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            process.WaitForExit();
            Task.WaitAll(outputTask, errorTask);

            var output = outputTask.Result;
            var error = errorTask.Result;

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
