using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using GuvenOn.App.Localization;
using GuvenOn.Core;
using GuvenOn.Core.Protection;
using GuvenOn.Core.Update;

namespace GuvenOn.App;

public partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _initializing = true;
    private bool _updateBusy;
    private bool _protectionBusy;
    private ProtectionStatus? _protectionStatus;
    private string? _downloadUrl;

    public MainWindow()
    {
        InitializeComponent();

        // SystemParameters.WorkArea is not consistently expressed in DIPs on
        // scaled displays. Clamp again after the native handle exists, using
        // the monitor work area and the window's actual DPI.
        WindowStartupLocation = WindowStartupLocation.Manual;
        SourceInitialized += (_, _) => ClampToWorkArea();

        BuildLanguageOptions();
        LoadSettings();

        VersionText.Text = Loc.Instance.Format("Version", AppInfo.VersionString);
        UpdateButton.Content = Loc.Instance["CheckForUpdates"];
        DownloadButton.Content = Loc.Instance["Download"];
        UpdateStatus.Text = Loc.Instance["UpdateIdle"];
        ProtectionButton.Content = Loc.Instance["ProtectionEnable"];
        PauseButton.Content = Loc.Instance["ProtectionPause"];

        Loaded += (_, _) =>
        {
            _initializing = false;
            RefreshAll();
        };

        Activated += (_, _) =>
        {
            RefreshServiceStatus();
            _ = RefreshProtectionAsync();
        };
    }

    private App AppHost => (App)Application.Current;

    private void BuildLanguageOptions()
    {
        LanguageBox.Items.Clear();
        LanguageBox.Items.Add(new LanguageOption(Loc.Auto, Loc.Instance["LanguageSystem"]));
        LanguageBox.Items.Add(new LanguageOption("en", "English"));
        LanguageBox.Items.Add(new LanguageOption("de", "Deutsch"));
        LanguageBox.Items.Add(new LanguageOption("tr", "Türkçe"));
    }

    private void RefreshLanguageSystemLabel()
    {
        var autoOption = LanguageBox.Items
            .OfType<LanguageOption>()
            .FirstOrDefault(option => option.Code == Loc.Auto);

        if (autoOption is not null)
        {
            autoOption.Name = Loc.Instance["LanguageSystem"];
        }
    }

    private void LoadSettings()
    {
        var language = Loc.NormalizeSetting(AppHost.Settings.Language);
        if (!string.Equals(language, AppHost.Settings.Language, StringComparison.Ordinal))
        {
            AppHost.Settings.Language = language;
            AppHost.SaveSettings();
        }

        LanguageBox.SelectedItem = LanguageBox.Items
            .OfType<LanguageOption>()
            .FirstOrDefault(option => option.Code == language) ?? LanguageBox.Items[0];

        var autostart = Autostart.IsEnabled();
        AutostartBox.IsChecked = autostart;

        if (AppHost.Settings.StartWithWindows != autostart)
        {
            AppHost.Settings.StartWithWindows = autostart;
            AppHost.SaveSettings();
        }
    }

    private void RefreshAll()
    {
        RefreshServiceStatus();
        RefreshNetwork();
        _ = RefreshProtectionAsync();
    }

    private void RefreshServiceStatus()
    {
        var status = QueryServiceStatus();

        switch (status)
        {
            case ServiceControllerStatus.Running:
                StatusDot.Fill = (Brush)FindResource("StatusGreenBrush");
                StatusTitle.Text = Loc.Instance["StatusServiceRunning"];
                StatusDetail.Text = Loc.Instance["StatusReadyDetail"];
                ServiceLine.Text = $"{Loc.Instance["Service"]}: {Loc.Instance["ServiceRunning"]}";
                break;

            case ServiceControllerStatus.Stopped:
            case ServiceControllerStatus.Paused:
                StatusDot.Fill = (Brush)FindResource("StatusAmberBrush");
                StatusTitle.Text = Loc.Instance["StatusServiceStopped"];
                StatusDetail.Text = Loc.Instance["StatusServiceStoppedDetail"];
                ServiceLine.Text = $"{Loc.Instance["Service"]}: {Loc.Instance["ServiceStopped"]}";
                break;

            case ServiceControllerStatus.StartPending:
            case ServiceControllerStatus.StopPending:
            case ServiceControllerStatus.PausePending:
            case ServiceControllerStatus.ContinuePending:
                StatusDot.Fill = (Brush)FindResource("StatusAmberBrush");
                StatusTitle.Text = Loc.Instance["StatusServicePending"];
                StatusDetail.Text = Loc.Instance["StatusServicePendingDetail"];
                ServiceLine.Text = $"{Loc.Instance["Service"]}: {Loc.Instance["ServicePending"]}";
                break;

            default:
                StatusDot.Fill = (Brush)FindResource("StatusAmberBrush");
                StatusTitle.Text = Loc.Instance["StatusServiceMissing"];
                StatusDetail.Text = Loc.Instance["StatusServiceMissingDetail"];
                ServiceLine.Text = $"{Loc.Instance["Service"]}: {Loc.Instance["ServiceNotInstalled"]}";
                break;
        }
    }

    private static ServiceControllerStatus? QueryServiceStatus()
    {
        try
        {
            using var controller = new ServiceController(AppInfo.ServiceName);
            return controller.Status;
        }
        catch
        {
            // Not installed, access denied, or the service database is unavailable.
            return null;
        }
    }

    private void RefreshNetwork()
    {
        try
        {
            var adapter = NetworkInterface.GetAllNetworkInterfaces()
                .Where(candidate =>
                    candidate.OperationalStatus == OperationalStatus.Up &&
                    candidate.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    candidate.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .OrderByDescending(candidate => candidate.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .FirstOrDefault();

            if (adapter is null)
            {
                NetworkName.Text = Loc.Instance["NetworkOffline"];
                NetworkType.Text = string.Empty;
                return;
            }

            NetworkName.Text = adapter.Name;

            var type = adapter.NetworkInterfaceType switch
            {
                NetworkInterfaceType.Wireless80211 => Loc.Instance["NetworkWifi"],
                NetworkInterfaceType.Ethernet => Loc.Instance["NetworkEthernet"],
                _ => adapter.NetworkInterfaceType.ToString()
            };

            var address = adapter.GetIPProperties().UnicastAddresses
                .FirstOrDefault(unicast => unicast.Address.AddressFamily == AddressFamily.InterNetwork)?
                .Address.ToString();

            NetworkType.Text = string.IsNullOrEmpty(address) ? type : $"{type} · {address}";
        }
        catch
        {
            NetworkName.Text = Loc.Instance["NetworkUnknown"];
            NetworkType.Text = string.Empty;
        }
    }

    private async Task RefreshProtectionAsync()
    {
        if (_protectionBusy)
        {
            return;
        }

        try
        {
            var response = await ProtectionClient.StatusAsync();
            UpdateProtectionUi(response.Status);
        }
        catch
        {
            UpdateProtectionUi(null);
        }
    }

    private void UpdateProtectionUi(ProtectionStatus? status)
    {
        _protectionStatus = status;

        if (_protectionBusy)
        {
            ProtectionButton.Content = Loc.Instance["ProtectionBusy"];
            ProtectionButton.IsEnabled = false;
            PauseButton.IsEnabled = false;
            return;
        }

        ProtectionButton.IsEnabled = true;
        PauseButton.IsEnabled = true;
        PauseButton.Visibility = Visibility.Collapsed;

        if (status is null)
        {
            ProtectionDot.Fill = (Brush)FindResource("StatusAmberBrush");
            ProtectionStateText.Text = Loc.Instance["ProtectionUnavailable"];
            ProtectionDetailText.Text = Loc.Instance["ProtectionUnavailableDetail"];
            ProtectionButton.Content = Loc.Instance["ProtectionEnable"];
            return;
        }

        if (status.EngineActive)
        {
            ProtectionDot.Fill = (Brush)FindResource("StatusGreenBrush");
            ProtectionStateText.Text = Loc.Instance["ProtectionOn"];
            ProtectionDetailText.Text = Loc.Instance["ProtectionOnDetail"];
            ProtectionButton.Content = Loc.Instance["ProtectionDisable"];
            PauseButton.Content = Loc.Instance["ProtectionPause"];
            PauseButton.Visibility = Visibility.Visible;
            return;
        }

        if (!string.IsNullOrWhiteSpace(status.LastError))
        {
            ProtectionDot.Fill = (Brush)FindResource("StatusRedBrush");
            ProtectionStateText.Text = Loc.Instance["ProtectionError"];
            ProtectionDetailText.Text = status.LastError;
            ProtectionButton.Content = Loc.Instance["ProtectionEnable"];
            return;
        }

        if (status.IsPaused(DateTimeOffset.UtcNow) && status.PausedUntil is { } pausedUntil)
        {
            ProtectionDot.Fill = (Brush)FindResource("StatusAmberBrush");
            ProtectionStateText.Text = Loc.Instance["ProtectionPaused"];
            ProtectionDetailText.Text = Loc.Instance.Format(
                "ProtectionPausedDetail",
                pausedUntil.LocalDateTime.ToString("HH:mm"));
            ProtectionButton.Content = Loc.Instance["ProtectionEnable"];
            return;
        }

        ProtectionDot.Fill = (Brush)FindResource("StatusAmberBrush");
        ProtectionStateText.Text = Loc.Instance["ProtectionOff"];
        ProtectionDetailText.Text = Loc.Instance["ProtectionOffDetail"];
        ProtectionButton.Content = Loc.Instance["ProtectionEnable"];
    }

    private async void Protection_Click(object sender, RoutedEventArgs e)
    {
        if (_protectionBusy)
        {
            return;
        }

        _protectionBusy = true;
        UpdateProtectionUi(_protectionStatus);

        try
        {
            var response = _protectionStatus?.EngineActive == true
                ? await ProtectionClient.DisableAsync(timeout: TimeSpan.FromSeconds(30))
                : await ProtectionClient.EnableAsync(timeout: TimeSpan.FromSeconds(30));

            UpdateProtectionUi(response.Status);

            if (!response.Ok && !string.IsNullOrWhiteSpace(response.Error))
            {
                ShowProtectionFailure(response.Error);
            }
        }
        catch (Exception exception)
        {
            UpdateProtectionUi(null);
            ShowProtectionFailure(exception.Message);
        }
        finally
        {
            _protectionBusy = false;
            UpdateProtectionUi(_protectionStatus);
        }
    }

    private async void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_protectionBusy)
        {
            return;
        }

        _protectionBusy = true;
        UpdateProtectionUi(_protectionStatus);

        try
        {
            var response = await ProtectionClient.PauseAsync(10, timeout: TimeSpan.FromSeconds(30));
            UpdateProtectionUi(response.Status);

            if (!response.Ok && !string.IsNullOrWhiteSpace(response.Error))
            {
                ShowProtectionFailure(response.Error);
            }
        }
        catch (Exception exception)
        {
            UpdateProtectionUi(null);
            ShowProtectionFailure(exception.Message);
        }
        finally
        {
            _protectionBusy = false;
            UpdateProtectionUi(_protectionStatus);
        }
    }

    private void ShowProtectionFailure(string error)
    {
        System.Windows.MessageBox.Show(
            this,
            Loc.Instance.Format("ProtectionFailed", error),
            Loc.Instance["Protection"],
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshAll();

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_updateBusy)
        {
            return;
        }

        _updateBusy = true;
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = Loc.Instance["Checking"];
        UpdateStatus.Text = Loc.Instance["Checking"];
        UpdateStatus.Foreground = (Brush)FindResource("TextSecondaryBrush");

        try
        {
            await AppHost.CheckForUpdatesAsync(showResult: true);
        }
        catch
        {
            ShowUpdateResult(Loc.Instance["UpdateCheckFailed"], isError: true);
        }
        finally
        {
            _updateBusy = false;
            UpdateButton.IsEnabled = true;
            UpdateButton.Content = Loc.Instance["CheckForUpdates"];
        }
    }

    public void ShowUpdateResult(string text, bool isError, string? downloadUrl = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => ShowUpdateResult(text, isError, downloadUrl));
            return;
        }

        _downloadUrl = UpdateChecker.IsTrustedDownloadUrl(downloadUrl) ? downloadUrl : null;

        UpdateStatus.Text = text;
        UpdateStatus.Foreground = (Brush)FindResource(isError ? "StatusRedBrush" : "StatusGreenBrush");
        DownloadButton.Content = Loc.Instance["Download"];
        DownloadButton.Visibility = _downloadUrl is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadUrl is null || !UpdateChecker.IsTrustedDownloadUrl(_downloadUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_downloadUrl) { UseShellExecute = true });
        }
        catch
        {
            ShowUpdateResult(Loc.Instance["DownloadFailed"], isError: true);
        }
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || LanguageBox.SelectedItem is not LanguageOption option)
        {
            return;
        }

        Loc.Instance.Apply(option.Code);
        RefreshLanguageSystemLabel();
        AppHost.RefreshTrayLocalization();
        AppHost.Settings.Language = option.Code;
        AppHost.SaveSettings();

        Title = Loc.Instance["AppName"];
        VersionText.Text = Loc.Instance.Format("Version", AppInfo.VersionString);
        UpdateButton.Content = _updateBusy ? Loc.Instance["Checking"] : Loc.Instance["CheckForUpdates"];
        DownloadButton.Content = Loc.Instance["Download"];
        _downloadUrl = null;
        DownloadButton.Visibility = Visibility.Collapsed;
        UpdateStatus.Text = _updateBusy ? Loc.Instance["Checking"] : Loc.Instance["UpdateIdle"];
        UpdateStatus.Foreground = (Brush)FindResource("TextSecondaryBrush");

        RefreshAll();
    }

    private void Autostart_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var enabled = AutostartBox.IsChecked == true;

        if (enabled)
        {
            if (!Autostart.Enable())
            {
                AutostartBox.IsChecked = false;
                return;
            }
        }
        else
        {
            Autostart.Disable();
        }

        AppHost.Settings.StartWithWindows = enabled;
        AppHost.SaveSettings();
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();

    private void Exit_Click(object sender, RoutedEventArgs e) => AppHost.ExitApplication();

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    public void AllowClose() => _allowClose = true;

    private void ClampToWorkArea()
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var dpi = GetDpiForWindow(handle);
        var scale = dpi <= 0 ? 1.0 : dpi / 96.0;
        var workWidth = (info.Work.Right - info.Work.Left) / scale;
        var workHeight = (info.Work.Bottom - info.Work.Top) / scale;
        var workLeft = info.Work.Left / scale;
        var workTop = info.Work.Top / scale;

        const double margin = 24;
        var availableWidth = Math.Max(320, workWidth - margin);
        var availableHeight = Math.Max(320, workHeight - margin);

        MinWidth = Math.Min(MinWidth, availableWidth);
        MinHeight = Math.Min(MinHeight, availableHeight);
        Width = Math.Min(Width, availableWidth);
        Height = Math.Min(Height, availableHeight);
        Left = workLeft + Math.Max(0, (workWidth - Width) / 2);
        Top = workTop + Math.Max(0, (workHeight - Height) / 2);
    }

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr handle);

    private sealed class LanguageOption : INotifyPropertyChanged
    {
        private string _name;

        public LanguageOption(string code, string name)
        {
            Code = code;
            _name = name;
        }

        public string Code { get; }

        public string Name
        {
            get => _name;
            set
            {
                if (string.Equals(_name, value, StringComparison.Ordinal))
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString() => Name;
    }
}
