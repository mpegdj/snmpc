using System.IO;
using System.Text.Json;

namespace SnmpNms.UI.Models;

public class AppPreferences
{
    public string? LastMapFilePath { get; set; }
    public bool AutoLoadLastMap { get; set; } = true;
    public string? DefaultCommunity { get; set; } = "public";
    public int DefaultTimeout { get; set; } = 3000;
    public int PollingInterval { get; set; } = 30;
    public bool AutoStartPolling { get; set; } = false;
    public int TrapListenerPort { get; set; } = 162;
    public bool EnableLogSave { get; set; } = true;
    public int MaxLogLines { get; set; } = 10000;
}

public static class PreferencesService
{
    private static readonly string PreferencesFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "SNMP");
    
    private static readonly string PreferencesFile = Path.Combine(PreferencesFolder, "preferences.json");

    public static AppPreferences Load()
    {
        try
        {
            if (!Directory.Exists(PreferencesFolder))
            {
                Directory.CreateDirectory(PreferencesFolder);
            }

            if (File.Exists(PreferencesFile))
            {
                var json = File.ReadAllText(PreferencesFile);
                return JsonSerializer.Deserialize<AppPreferences>(json) ?? new AppPreferences();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Preferences] Load error: {ex.Message}");
        }

        return new AppPreferences();
    }

    public static void Save(AppPreferences preferences)
    {
        try
        {
            if (!Directory.Exists(PreferencesFolder))
            {
                Directory.CreateDirectory(PreferencesFolder);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(preferences, options);
            File.WriteAllText(PreferencesFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Preferences] Save error: {ex.Message}");
        }
    }

    public static string GetSnmpFolder() => PreferencesFolder;
}
