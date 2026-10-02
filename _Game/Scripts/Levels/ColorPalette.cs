using UnityEngine;

[CreateAssetMenu(
    fileName = "ColorPalette",
    menuName = "ColorLoop/Color Palette")]
public sealed class ColorPalette : ScriptableObject
{
    [SerializeField] private Color[] colors =
    {
        new Color32(239, 83, 80, 255),   // Kırmızı
        new Color32(66, 165, 245, 255),  // Mavi
        new Color32(255, 202, 40, 255),  // Sarı
        new Color32(102, 187, 106, 255), // Yeşil
        new Color32(171, 71, 188, 255),  // Mor
        new Color32(255, 167, 38, 255)   // Turuncu
    };

    public int Count => colors.Length;

    public Color GetColor(int colorId)
    {
        return colors[colorId];
    }

    public bool Contains(int colorId)
    {
        return colorId >= 0 && colorId < colors.Length;
    }
}
