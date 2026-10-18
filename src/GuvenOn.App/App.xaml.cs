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

        _mutex = new Mutex(initiallyOwned: true, @"Local\GuvenOn.App", out var createdNew);
        if (!createdNew)
        {
            // Bring the existing instance forward, then leave quietly.
            try
            {
                EventWaitHandle.OpenExisting(@"Local\GuvenOn.App.Show").Set();
            }
            catch
            {
                // The other instance is shutting down.
            }

            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\GuvenOn.App.Show");
        _showLoopCancellation = new CancellationTokenSource();
        _ = Task.Run(() => ListenForShowRequests(_showLoopCancellation.Token));

        _store = new SettingsStore();
        _settings = _store.Load();

        var language = _settings.Language == "auto" ? Loc.DetectLanguage() : _settings.Language;
        Loc.Instance.Apply(language);

        _window = new MainWindow();
        _tray = new TrayIcon(ShowWindow, ExitApplication, () => _ = CheckForUpdatesAsync(showResult: true));
        _tray.SetTooltip(Loc.Instance.Format("TrayTooltip", AppInfo.VersionString));

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
        if (_settings.LastUpdateCheck is null ||
            DateTimeOffset.UtcNow - _settings.LastUpdateCheck > TimeSpan.FromHours(24))
        {
            _ = CheckForUpdatesAsync(showResult: false);
        }
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

    public async Task CheckForUpdatesAsync(bool showResult)
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

    public void ExitApplication()
    {
        _showLoopCancellation?.Cancel();
        _showLoopCancellation?.Dispose();
        _showLoopCancellation = null;

        _window?.AllowClose();
        _tray?.Dispose();
        _tray = null;

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
        _tray?.Dispose();
        _showEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
