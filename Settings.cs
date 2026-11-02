using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DropMyShelf;

/// <summary>User preferences, saved to %APPDATA%\DropMyShelf\settings.json.</summary>
internal sealed class Settings
{
    public static readonly int[] HistorySizeChoices = { 3, 5, 10, 20, 50 };
    public const int DefaultHistorySize = 5;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DropMyShelf", "settings.json");

    public int HistorySize { get; set; } = DefaultHistorySize;

    /// <summary>Look for a newer release on GitHub at startup and once a day.</summary>
    public bool AutoCheckUpdates { get; set; } = true;

    /// <summary>Last version the user was told about, so each release is announced only once.</summary>
    public string? NotifiedVersion { get; set; }

    /// <summary>The "DropMyShelf is running" tip has been shown once.</summary>
    public bool WelcomeShown { get; set; }

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) is { } settings)
            {
                settings.HistorySize = Math.Clamp(settings.HistorySize, 1, HistorySizeChoices[^1]);
                return settings;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DropMyShelf: could not read settings: {ex}");
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DropMyShelf: could not save settings: {ex}");
        }
    }
}
