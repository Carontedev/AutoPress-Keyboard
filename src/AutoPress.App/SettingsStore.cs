using System.Text.Json;
using AutoPress.Windows;

namespace AutoPress.App;

public sealed record AppSettings
{
    public int Version { get; init; } = 2;
    public int Key { get; init; } = 0x46;
    public int IntervalMs { get; init; } = 100;
    public string LimitMode { get; init; } = "Duration";
    public int LimitValue { get; init; } = 300;
    public bool Unlimited { get; init; }
    public Hotkey Hotkey { get; init; } = Hotkey.Default;
    public string Theme { get; init; } = "System";
    public string? TargetPath { get; init; }
}

public static class SettingsStore
{
    public static string DirectoryPath => Environment.GetEnvironmentVariable("AUTOPRESS_SETTINGS_DIRECTORY")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoPressKeyboard", "settings");
    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public static (AppSettings Settings, string? Warning) Load()
    {
        if (!File.Exists(FilePath)) return (new(), null);
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
            if (settings is null || settings.Version is not (1 or 2) || !settings.Hotkey.IsValid) throw new JsonException();
            return (settings with
            {
                Version = 2,
                // Earlier releases stored the built-in monitor as this executable.
                TargetPath = string.Equals(settings.TargetPath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)
                    ? null : settings.TargetPath,
                LimitMode = settings.LimitMode switch
                {
                    "Tiempo" or "Duration" => "Duration",
                    "Pulsaciones" or "Keystrokes" => "Keystrokes",
                    _ => throw new JsonException()
                },
                Theme = settings.Theme switch
                {
                    "Sistema" or "System" => "System",
                    "Claro" or "Light" => "Light",
                    "Oscuro" or "Dark" => "Dark",
                    _ => throw new JsonException()
                }
            }, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return (new(), "Settings could not be loaded. Default settings have been restored.");
        }
    }
    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        string temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
}
