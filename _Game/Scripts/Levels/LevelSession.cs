using System;
using UnityEngine;

public static class LevelSession
{
    private const string ProgressKey = "ColorLoop.CurrentLevel";

    private static LevelCatalog catalog;

    public static LevelData CurrentLevel { get; private set; }
    public static int CurrentIndex { get; private set; }

    public static bool HasNextLevel =>
        catalog != null &&
        CurrentIndex + 1 < catalog.Count;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        catalog = null;
        CurrentLevel = null;
        CurrentIndex = 0;
    }

    public static void Initialize(LevelCatalog levelCatalog)
    {
        if (levelCatalog == null || levelCatalog.Count == 0)
        {
            throw new InvalidOperationException(
                "LevelCatalog atanmamış veya bölüm listesi boş.");
        }

        for (int i = 0; i < levelCatalog.Count; i++)
        {
            LevelData level = levelCatalog.GetLevel(i);

            if (level == null || !level.HasBoardData)
            {
                throw new InvalidOperationException(
                    $"LevelCatalog: {i}. bölüm boş veya tahta verisi geçersiz.");
            }
        }

        catalog = levelCatalog;

        CurrentIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(ProgressKey, 0),
            0,
            catalog.Count - 1);

        CurrentLevel = catalog.GetLevel(CurrentIndex);
    }

    public static bool TryAdvance()
    {
        if (!HasNextLevel)
            return false;

        CurrentIndex++;
        CurrentLevel = catalog.GetLevel(CurrentIndex);

        PlayerPrefs.SetInt(ProgressKey, CurrentIndex);
        PlayerPrefs.Save();

        return true;
    }

    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(ProgressKey);
        PlayerPrefs.Save();

        CurrentIndex = 0;
        CurrentLevel = catalog != null ? catalog.GetLevel(0) : null;
    }
}