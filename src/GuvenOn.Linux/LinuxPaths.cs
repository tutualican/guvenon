namespace GuvenOn.Linux;

internal static class LinuxPaths
{
    public static string StateDirectory =>
        Environment.GetEnvironmentVariable("GUVENON_STATE_DIR") ?? "/var/lib/guvenon";

    public static string RuntimeDirectory =>
        Environment.GetEnvironmentVariable("GUVENON_RUNTIME_DIR") ?? "/run/guvenon";

    public static string StateFile => Path.Combine(StateDirectory, "state.json");

    public static string ControlSocket => Path.Combine(RuntimeDirectory, "control.sock");

    public static string ResolvedDropIn => "/etc/systemd/resolved.conf.d/90-guvenon.conf";

    public static string ResolvedDropInBackup =>
        Path.Combine(StateDirectory, "resolved.conf.d-90-guvenon.conf.bak");

    public static string ResolvConf => "/etc/resolv.conf";

    public static string ResolvConfBackup => Path.Combine(StateDirectory, "resolv.conf.bak");
}
