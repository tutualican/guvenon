using System.ComponentModel;
using System.Drawing;
using System.IO;
using GuvenOn.App.Localization;

namespace GuvenOn.App.Tray;

/// <summary>
/// Thin wrapper around the WinForms NotifyIcon. WinForms ships with the
/// Windows Desktop runtime, so the tray adds no third-party dependency.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly Icon _appIcon;
    private readonly bool _ownsIcon;
    private readonly Action _open;
    private readonly Action _exit;
    private readonly Action _checkUpdates;
    private bool _disposed;
    private Font? _boldFont;

    public TrayIcon(Action open, Action exit, Action checkUpdates)
    {
        _open = open;
        _exit = exit;
        _checkUpdates = checkUpdates;

        _menu = new ContextMenuStrip();
        (_appIcon, _ownsIcon) = LoadIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Visible = true,
            ContextMenuStrip = _menu
        };

        _notifyIcon.DoubleClick += (_, _) => _open();
        Loc.Instance.PropertyChanged += OnLocalizationChanged;
        RebuildMenu();
    }

    public void SetTooltip(string text)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
        }
        catch
        {
            // Explorer may be restarting; the next update will succeed.
        }
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = text;
            _notifyIcon.BalloonTipIcon = icon;
            _notifyIcon.ShowBalloonTip(5000);
        }
        catch
        {
            // Notifications are optional.
        }
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]")
        {
            RebuildMenu();
        }
    }

    private void RebuildMenu()
    {
        if (_disposed)
        {
            return;
        }

        _menu.SuspendLayout();
        try
        {
            var oldItems = _menu.Items.Cast<ToolStripItem>().ToArray();
            _menu.Items.Clear();
            foreach (var item in oldItems)
            {
                item.Dispose();
            }

            _boldFont?.Dispose();
            _boldFont = new Font(_menu.Font, FontStyle.Bold);

            var open = new ToolStripMenuItem(Loc.Instance["Open"])
            {
                Font = _boldFont
            };
            open.Click += (_, _) => _open();
            _menu.Items.Add(open);

            var updates = new ToolStripMenuItem(Loc.Instance["CheckForUpdates"]);
            updates.Click += (_, _) => _checkUpdates();
            _menu.Items.Add(updates);

            _menu.Items.Add(new ToolStripSeparator());

            var exit = new ToolStripMenuItem(Loc.Instance["Exit"]);
            exit.Click += (_, _) => _exit();
            _menu.Items.Add(exit);
        }
        finally
        {
            _menu.ResumeLayout();
        }
    }

    private static (Icon Icon, bool Owned) LoadIcon()
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (executable is { Length: > 0 } && File.Exists(executable))
            {
                var extracted = Icon.ExtractAssociatedIcon(executable);
                if (extracted is not null)
                {
                    return (extracted, true);
                }
            }
        }
        catch
        {
            // Fall back to the stock shield if the executable icon is unavailable.
        }

        return (SystemIcons.Shield, false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loc.Instance.PropertyChanged -= OnLocalizationChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _boldFont?.Dispose();
        _boldFont = null;

        if (_ownsIcon)
        {
            _appIcon.Dispose();
        }
    }
}
