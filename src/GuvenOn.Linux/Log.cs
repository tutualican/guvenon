namespace GuvenOn.Linux;

internal static class Log
{
    public static void Info(string message) =>
        Console.WriteLine($"{DateTimeOffset.Now:O} info: {message}");

    public static void Warning(string message) =>
        Console.Error.WriteLine($"{DateTimeOffset.Now:O} warning: {message}");

    public static void Error(string message) =>
        Console.Error.WriteLine($"{DateTimeOffset.Now:O} error: {message}");
}
