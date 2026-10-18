using Microsoft.Win32;

namespace GuvenOn.Core;

/// <summary>
/// Per-user autostart. Writing HKCU never requires elevation, which keeps the
/// tray app free of UAC prompts after installation.
/// </summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GuvenOn";

    public static string Command
    {
        get
        {
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "guvenon.exe");
            return $"\"{exe}\" --tray";
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static bool Enable()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            key?.SetValue(ValueName, Command, RegistryValueKind.String);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // Best effort: the value may not exist or the hive may be read-only.
        }
    }

    public static void Sync(bool enabled)
    {
        if (enabled)
        {
            Enable();
        }
        else
        {
            Disable();
        }
    }
}
