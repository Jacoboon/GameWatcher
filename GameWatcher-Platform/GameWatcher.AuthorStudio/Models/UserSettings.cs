using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameWatcher.AuthorStudio.Models;

/// <summary>
/// User preferences and session state persisted across app launches.
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Last opened pack folder path - auto-loads on startup.
    /// </summary>
    [JsonPropertyName("lastPackPath")]
    public string? LastPackPath { get; set; }

    /// <summary>
    /// Recently opened pack paths (up to 10).
    /// </summary>
    [JsonPropertyName("recentPacks")]
    public List<string> RecentPacks { get; set; } = new();

    /// <summary>
    /// Main window width (0 = use default).
    /// </summary>
    [JsonPropertyName("windowWidth")]
    public double WindowWidth { get; set; }

    /// <summary>
    /// Main window height (0 = use default).
    /// </summary>
    [JsonPropertyName("windowHeight")]
    public double WindowHeight { get; set; }

    /// <summary>
    /// Main window X position (null = centered).
    /// </summary>
    [JsonPropertyName("windowLeft")]
    public double? WindowLeft { get; set; }

    /// <summary>
    /// Main window Y position (null = centered).
    /// </summary>
    [JsonPropertyName("windowTop")]
    public double? WindowTop { get; set; }

    /// <summary>
    /// Whether to auto-load the last pack on startup.
    /// </summary>
    [JsonPropertyName("autoLoadLastPack")]
    public bool AutoLoadLastPack { get; set; } = true;

    /// <summary>
    /// Maximum number of recent packs to remember.
    /// </summary>
    [JsonIgnore]
    public const int MaxRecentPacks = 10;
}
