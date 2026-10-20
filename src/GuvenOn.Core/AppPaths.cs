namespace GuvenOn.Core;

public static class AppPaths
{
    public static string ProgramData { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GuvenOn");

    public static string UserData { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuvenOn");

    public static string Logs => Path.Combine(ProgramData, "logs");
    public static string ServiceSettings => Path.Combine(ProgramData, "service.json");
    public static string ServiceState => Path.Combine(ProgramData, "service-state.json");
    public static string UserSettings => Path.Combine(UserData, "settings.json");
    public static string UpdatesCache => Path.Combine(ProgramData, "updates");

    public static string EnsureUserData()
    {
        Directory.CreateDirectory(UserData);
        return UserData;
    }

    public static string EnsureProgramData()
    {
        Directory.CreateDirectory(ProgramData);
        return ProgramData;
    }
}
