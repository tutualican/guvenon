using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace GuvenOn.App.Localization;

/// <summary>
/// Source-language resource lookup for the UI. The service and CLI never use
/// this type; they emit English identifiers and parameters only.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public const string Auto = "auto";

    private static readonly ResourceManager Resources =
        new("GuvenOn.App.Resources.Strings", typeof(Loc).Assembly);

    // Capture the Windows display language before Apply() overwrites the
    // thread culture. Otherwise selecting "System language" after choosing a
    // language manually would simply resolve back to the manual choice.
    private static readonly string SystemLanguage = ResolveLanguage(CultureInfo.CurrentUICulture);

    public static Loc Instance { get; } = new();

    private string _language = "en";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language => _language;

    public string this[string key] => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentUICulture, this[key], arguments);

    public static string DetectLanguage() => SystemLanguage;

    public static string NormalizeSetting(string? language)
    {
        var code = language?.Trim().ToLowerInvariant();
        return code is "en" or "de" or "tr" or Auto ? code : Auto;
    }

    private static string ResolveLanguage(CultureInfo culture)
    {
        var code = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        return code is "de" or "tr" ? code : "en";
    }

    public void Apply(string language)
    {
        var requested = language?.Trim().ToLowerInvariant();
        var code = requested is "en" or "de" or "tr" ? requested : DetectLanguage();
        var culture = CultureInfo.GetCultureInfo(code switch
        {
            "de" => "de-DE",
            "tr" => "tr-TR",
            _ => "en-US"
        });

        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;

        _language = code;

        // "Item[]" refreshes every indexer binding in the visual tree.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }
}
