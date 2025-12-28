using System.Collections.Generic;

namespace SnmpNms.UI.Models;

/// <summary>
/// NTT 계열 장비(MVE, NTT 제품 등)의 트랩 정보를 번역하기 위한 매퍼 클래스
/// </summary>
public static class NttTrapMappers
{
    private static readonly Dictionary<int, string> LevelMap = new()
    {
        { 0, "None" },
        { 1, "Emergency" },
        { 2, "Alert" },
        { 3, "Critical" },
        { 4, "Error" },
        { 5, "Warning" }, // CSV: Warn
        { 6, "Notice" },
        { 7, "Info" }
    };

    private static readonly Dictionary<int, string> CategoryMap = new()
    {
        { 0, "None" },
        { 1, "Power" },
        { 2, "General" },
        { 3, "InOut" },
        { 4, "Video" },
        { 5, "Audio" },
        { 6, "IP" },
        { 7, "Network" },
        { 8, "Pref" },
        { 9, "Device" }
    };

    public static string GetLevelName(int level)
    {
        return LevelMap.TryGetValue(level, out var name) ? name : level.ToString();
    }

    public static string GetCategoryName(int category)
    {
        return CategoryMap.TryGetValue(category, out var name) ? name : category.ToString();
    }

    public static string GetLevelName(string? levelStr)
    {
        if (int.TryParse(levelStr, out int level)) return GetLevelName(level);
        return levelStr ?? "Unknown";
    }

    public static string GetCategoryName(string? categoryStr)
    {
        if (int.TryParse(categoryStr, out int category)) return GetCategoryName(category);
        return categoryStr ?? "Unknown";
    }
}
