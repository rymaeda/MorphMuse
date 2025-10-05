using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class PluginSettings
{
    public string ActiveProfile { get; set; } = "default";
    public Dictionary<string, SettingsProfile> Profiles { get; set; } = new();

    private static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MorphMuse", "settings.json");

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static SettingsRepository Load()
    {
        if (!File.Exists(ConfigPath))
            return new SettingsRepository();

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<SettingsRepository>(json) ?? new SettingsRepository();
    }

    public SettingsProfile GetActiveProfile()
    {
        return Profiles.ContainsKey(ActiveProfile) ? Profiles[ActiveProfile] : new SettingsProfile();
    }
}