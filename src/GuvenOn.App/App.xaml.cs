using System.IO;
using System.Windows;
using Application = System.Windows.Application;
using GuvenOn.App.Localization;
using GuvenOn.App.Tray;
using GuvenOn.Core;
using GuvenOn.Core.Settings;
using GuvenOn.Core.Update;

namespace GuvenOn.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private CancellationTokenSource? _showLoopCancellation;
    private SettingsStore _store = null!;
    private AppSettings _settings = null!;
    private TrayIcon? _tray;
    private MainWindow? _window;
    private readonly UpdateChecker _updates = new();

    public AppSettings Settings => _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (TryRunMaintenance(e.Args, out var maintenanceExitCode))
        {
            Shutdown(maintenanceExitCode);
            return;
        }

        // Create the show event before the mutex. Whichever instance wins the
        // mutex can then always receive a show request, even during startup.
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\GuvenOn.App.Show");
        _mutex = new Mutex(initiallyOwned: true, @"Local\GuvenOn.App", out var createdNew);
        if (!createdNew)
        {
            // Bring the existing instance forward, then leave quietly.
            try
            {
                _showEvent.Set();
            }
            catch
            {
                // The other instance is shutting down.
            }

            _showEvent.Dispose();
            _showEvent = null;
            Shutdown();
            return;
        }

        _store = new SettingsStore();
        _settings = _store.Load();

        var configuredLanguage = Loc.NormalizeSetting(_settings.Language);
        if (!string.Equals(configuredLanguage, _settings.Language, StringComparison.Ordinal))
        {
            _settings.Language = configuredLanguage;
            _store.Save(_settings);
        }

        Loc.Instance.Apply(configuredLanguage);

        _window = new MainWindow();
        _tray = new TrayIcon(ShowWindow, ExitApplication, () => _ = CheckForUpdatesAsync(showResult: true));
        _tray.SetTooltip(Loc.Instance.Format("TrayTooltip", AppInfo.VersionString));

        // Start listening only after the window exists so a request that arrived
        // during startup is not dropped.
        _showLoopCancellation = new CancellationTokenSource();
        _ = Task.Run(() => ListenForShowRequests(_showLoopCancellation.Token));

        var startInTray = e.Args.Any(argument =>
            string.Equals(argument, "--tray", StringComparison.OrdinalIgnoreCase));

        if (startInTray)
        {
            _tray.ShowBalloon(Loc.Instance["TrayBalloonTitle"], Loc.Instance["TrayBalloonStarted"]);
        }
        else
        {
            ShowWindow();
        }

        // One check per day at most. The manifest request does not use the GitHub API.
        var now = DateTimeOffset.UtcNow;
        if (_settings.LastUpdateCheck is null ||
            _settings.LastUpdateCheck > now ||
            now - _settings.LastUpdateCheck >= TimeSpan.FromHours(24))
        {
            _ = CheckForUpdatesAsync(showResult: false);
        }
    }

    /// <summary>
    /// Installer helpers run as the signed-in user, so HKCU autostart and
    /// per-user settings are written to the right profile even when setup was
    /// elevated with a different administrator account.
    /// </summary>
    private static bool TryRunMaintenance(string[] arguments, out int exitCode)
    {
        exitCode = 0;

        var enableAutostart = arguments.Any(argument =>
            string.Equals(argument, "--enable-autostart", StringComparison.OrdinalIgnoreCase));
        var disableAutostart = arguments.Any(argument =>
            string.Equals(argument, "--disable-autostart", StringComparison.OrdinalIgnoreCase));
        var cleanupUser = arguments.Any(argument =>
            string.Equals(argument, "--cleanup-user", StringComparison.OrdinalIgnoreCase));

        if (!enableAutostart && !disableAutostart && !cleanupUser)
        {
            return false;
        }

        var store = new SettingsStore();
        var settings = store.Load();

        if (enableAutostart)
        {
            settings.StartWithWindows = Autostart.Enable();
            if (!settings.StartWithWindows)
            {
                // Let setup report that the per-user startup entry could not be written.
                exitCode = 1;
            }
        }
        else
        {
            Autostart.Disable();
            settings.StartWithWindows = false;
        }

        store.Save(settings);

        if (cleanupUser)
        {
            try
            {
                if (Directory.Exists(AppPaths.UserData))
                {
                    Directory.Delete(AppPaths.UserData, recursive: true);
                }
            }
            catch
            {
                // Cleanup is best effort; never keep the helper alive because a
                // locked file could not be removed.
            }
        }

        return true;
    }

    private async Task ListenForShowRequests(CancellationToken cancellationToken)
    {
        var eventHandle = _showEvent;
        if (eventHandle is null)
        {
            return;
        }

        var handles = new WaitHandle[] { eventHandle, cancellationToken.WaitHandle };

        try
        {
            while (WaitHandle.WaitAny(handles) == 0)
            {
                await Dispatcher.InvokeAsync(ShowWindow);
            }
        }
        catch (ObjectDisposedException)
        {
            // Application shutdown.
        }
    }

    public void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    public void SaveSettings() => _store.Save(_settings);

    public void RefreshTrayLocalization()
    {
        _tray?.SetTooltip(Loc.Instance.Format("TrayTooltip", AppInfo.VersionString));
    }

    public async Task CheckForUpdatesAsync(bool showResult)
    {
        try
        {
            var result = await _updates.CheckAsync();

            if (result.Success)
            {
                _settings.LastUpdateCheck = DateTimeOffset.UtcNow;
                _store.Save(_settings);
            }

            if (result.UpdateAvailable && result.Manifest is not null)
            {
                var text = Loc.Instance.Format("UpdateAvailable", result.Manifest.Version);
                _tray?.ShowBalloon(Loc.Instance["TrayBalloonTitle"], text);
                _window?.ShowUpdateResult(text, isError: false, downloadUrl: result.Manifest.Url);
            }
            else if (showResult)
            {
                _window?.ShowUpdateResult(
                    result.Success ? Loc.Instance["UpToDate"] : Loc.Instance["UpdateCheckFailed"],
                    isError: !result.Success);
            }
        }
        catch
        {
            if (showResult)
            {
                _window?.ShowUpdateResult(Loc.Instance["UpdateCheckFailed"], isError: true);
            }
        }
    }

    public void ExitApplication()
    {
        _showLoopCancellation?.Cancel();

        _window?.AllowClose();
        _tray?.Dispose();
        _tray = null;

        _showLoopCancellation?.Dispose();
        _showLoopCancellation = null;

        _showEvent?.Dispose();
        _showEvent = null;

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // The mutex was already released during shutdown.
        }

        _mutex?.Dispose();
        _mutex = null;

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showLoopCancellation?.Cancel();
        _showLoopCancellation?.Dispose();
        _showLoopCancellation = null;

        _tray?.Dispose();
        _tray = null;
        _showEvent?.Dispose();
        _showEvent = null;
        _mutex?.Dispose();
        _mutex = null;

        base.OnExit(e);
    }
}
