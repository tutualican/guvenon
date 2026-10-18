using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using GuvenOn.App.Localization;
using GuvenOn.Core;

namespace GuvenOn.App;

public partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _initializing = true;
    private bool _updateBusy;
    private string? _downloadUrl;

    public MainWindow()
    {
        InitializeComponent();

        // Keep the window inside the work area on scaled displays.
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, workArea.Width - 40);
        Height = Math.Min(Height, workArea.Height - 40);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + (workArea.Height - Height) / 2;

        BuildLanguageOptions();
        LoadSettings();

        VersionText.Text = Loc.Instance.Format("Version", AppInfo.VersionString);
        UpdateButton.Content = Loc.Instance["CheckForUpdates"];
        DownloadButton.Content = Loc.Instance["Download"];
        UpdateStatus.Text = Loc.Instance["UpdateIdle"];

        Loaded += (_, _) =>
        {
            _initializing = false;
            RefreshAll();
        };

        Activated += (_, _) => RefreshServiceStatus();
    }

    private App AppHost => (App)Application.Current;

    private void BuildLanguageOptions()
    {
        LanguageBox.Items.Clear();
        LanguageBox.Items.Add(new LanguageOption("en", "English"));
        LanguageBox.Items.Add(new LanguageOption("de", "Deutsch"));
        LanguageBox.Items.Add(new LanguageOption("tr", "Türkçe"));
    }

    private void LoadSettings()
    {
        var language = Loc.Instance.Language;
        foreach (var option in LanguageBox.Items.OfType<LanguageOption>())
        {
            if (option.Code == language)
            {
                LanguageBox.SelectedItem = option;
                break;
            }
        }

        AutostartBox.IsChecked = Autostart.IsEnabled();
    }

    private void RefreshAll()
    {
        RefreshServiceStatus();
        RefreshNetwork();
    }

    private void RefreshServiceStatus()
    {
        var status = QueryServiceStatus();

        switch (status)
        {
            case ServiceControllerStatus.Running:
                StatusDot.Fill = (Brush)FindResource("StatusGreenBrush");
                StatusTitle.Text = Loc.Instance["StatusServiceRunning"];
                StatusDetail.Text = Loc.Instance["StatusPreviewDetail"];
                ServiceLine.Text = $"{Loc.Instance["Service"]}: {Loc.Instance["ServiceRunning"]}";
                break;

            case ServiceControllerStatus.Stopped:
            case ServiceControllerStatus.PausePending:
            case ServiceControllerStatus.Paused:
                StatusDot.Fill = (Brush)FindResource("StatusAmberBrush");
                StatusTitle.Text = Loc.Instance["StatusServiceStopped"];
                StatusDetail.Text = Loc.Instance["StatusServiceStoppedDetail"];
                ServiceLine.Text = $"{Loc.Instance["Service"]}: {Loc.Instance["ServiceStopped"]}";
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

        _downloadUrl = IsTrustedDownloadUrl(downloadUrl) ? downloadUrl : null;

        UpdateStatus.Text = text;
        UpdateStatus.Foreground = (Brush)FindResource(isError ? "StatusRedBrush" : "StatusGreenBrush");
        DownloadButton.Content = Loc.Instance["Download"];
        DownloadButton.Visibility = _downloadUrl is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private static bool IsTrustedDownloadUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("dl.guvenon.app", StringComparison.OrdinalIgnoreCase);
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadUrl is null || !IsTrustedDownloadUrl(_downloadUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_downloadUrl) { UseShellExecute = true });
        }
        catch
        {
            ShowUpdateResult(Loc.Instance["UpdateCheckFailed"], isError: true);
        }
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || LanguageBox.SelectedItem is not LanguageOption option)
        {
            return;
        }

        Loc.Instance.Apply(option.Code);
        AppHost.Settings.Language = option.Code;
        AppHost.SaveSettings();

        Title = Loc.Instance["AppName"];
        VersionText.Text = Loc.Instance.Format("Version", AppInfo.VersionString);
        UpdateButton.Content = Loc.Instance["CheckForUpdates"];
        DownloadButton.Content = Loc.Instance["Download"];
        UpdateStatus.Text = Loc.Instance["UpdateIdle"];
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

    private sealed record LanguageOption(string Code, string Name)
    {
        public override string ToString() => Name;
    }
}
