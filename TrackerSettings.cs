using System;
using System.IO;
using System.Text.Json;

namespace GorillaShinyRox;

public sealed class TrackerSettings
{
    public int CurrentRox { get; set; } = 3500;
    public int GoalRox { get; set; } = 11400;
    public int RoxPerReward { get; set; } = 100;
    public int RewardCycleHours { get; set; } = 24;
    public DateTime NextRewardUtc { get; set; } = DateTime.UtcNow;
    public bool IsTimerSet { get; set; } = false;
    public bool HasSeenTips { get; set; } = false;

    public static string FolderPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GorillaShinyRox");

    public static string FilePath => Path.Combine(FolderPath, "settings.json");

    public static TrackerSettings Load()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);

            if (!File.Exists(FilePath))
            {
                TrackerSettings defaults = new();
                defaults.Save();
                return defaults;
            }

            string json = File.ReadAllText(FilePath);
            TrackerSettings settings = JsonSerializer.Deserialize<TrackerSettings>(json) ?? new TrackerSettings();
            settings.Validate();
            return settings;
        }
        catch
        {
            return new TrackerSettings();
        }
    }

    public void Validate()
    {
        if (CurrentRox < 0) CurrentRox = 0;
        if (GoalRox < 0) GoalRox = 0;
        if (RoxPerReward <= 0) RoxPerReward = 1;
        if (RewardCycleHours <= 0) RewardCycleHours = 1;
        if (NextRewardUtc == default) NextRewardUtc = DateTime.UtcNow;
    }

    public void Save()
    {
        Validate();
        Directory.CreateDirectory(FolderPath);

        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
