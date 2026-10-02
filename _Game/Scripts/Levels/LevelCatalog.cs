using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelCatalog",
    menuName = "ColorLoop/Level Catalog")]
public sealed class LevelCatalog : ScriptableObject
{
    [SerializeField] private LevelData[] levels;

    public int Count => levels != null ? levels.Length : 0;

    public LevelData GetLevel(int index)
    {
        if (levels == null || index < 0 || index >= levels.Length)
            return null;

        return levels[index];
    }
}