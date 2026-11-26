using System.IO;
using System.Text.Json;

namespace Pulse;

public sealed class GamePreferences
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Pulse",
        "game.json");

    public string Language { get; set; } = "pl";
    public string? LastGameName { get; set; }
    public string? LastGameTitle { get; set; }

    public static GamePreferences Load()
    {
        if (!File.Exists(SettingsPath))
            return new GamePreferences();

        return JsonSerializer.Deserialize<GamePreferences>(File.ReadAllText(SettingsPath))
               ?? new GamePreferences();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
