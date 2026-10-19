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
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe) ||
                string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                // Under `dotnet run` (or the test host) ProcessPath is dotnet.exe,
                // which would produce an autostart command that cannot launch the app.
                exe = Path.Combine(AppContext.BaseDirectory, "guvenon.exe");
            }

            return $"\"{exe}\" --tray";
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string value && IsLaunchCommand(value);
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
            if (key is null)
            {
                return false;
            }

            key.SetValue(ValueName, Command, RegistryValueKind.String);
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

    /// <summary>
    /// A Run value is only useful when it points at an existing Guven-ON
    /// executable and passes the tray flag. A stale value from an older install
    /// must not make the settings switch look enabled.
    /// </summary>
    public static bool IsLaunchCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var text = command.Trim();
        string executable;
        string arguments;

        if (text.StartsWith('"'))
        {
            var closingQuote = text.IndexOf('"', 1);
            if (closingQuote <= 1)
            {
                return false;
            }

            executable = text[1..closingQuote];
            arguments = text[(closingQuote + 1)..];
        }
        else
        {
            var separator = text.IndexOfAny([' ', '\t']);
            executable = separator < 0 ? text : text[..separator];
            arguments = separator < 0 ? string.Empty : text[separator..];
        }

        if (!string.Equals(Path.GetFileName(executable), "guvenon.exe", StringComparison.OrdinalIgnoreCase) ||
            !Path.IsPathFullyQualified(executable) ||
            !File.Exists(executable))
        {
            return false;
        }

        return arguments
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Any(argument => string.Equals(argument, "--tray", StringComparison.OrdinalIgnoreCase));
    }
}
