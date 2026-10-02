using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PixelArtImporter
{
    private const int MaxSize = 128;
    private const byte AlphaThreshold = 128;

    public static void Import(LevelData level)
    {
        if (level == null ||
            level.Palette == null ||
            level.Palette.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Eksik veri",
                "Önce bölüme dolu bir renk paleti ata.",
                "Tamam");

            return;
        }

        string path = EditorUtility.OpenFilePanel(
            "Pixel art PNG seç", "", "png");

        if (string.IsNullOrEmpty(path))
            return;

        Texture2D texture = null;

        try
        {
            texture = new Texture2D(
                2, 2, TextureFormat.RGBA32, false);

            bool loaded = ImageConversion.LoadImage(
                texture,
                File.ReadAllBytes(path),
                false);

            if (!loaded)
            {
                throw new InvalidOperationException(
                    "Resim okunamadı.");
            }

            if (texture.width > MaxSize || texture.height > MaxSize)
            {
                EditorUtility.DisplayDialog(
                    "Resim çok büyük",
                    $"En fazla {MaxSize} × {MaxSize} PNG kullan. " +
                    $"Seçilen resim: {texture.width} × {texture.height}",
                    "Tamam");

                return;
            }

            Color32[] sourcePixels = texture.GetPixels32();

            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;

            // Görünür piksellerin sınırlarını bul.
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    int index = y * texture.width + x;

                    if (sourcePixels[index].a < AlphaThreshold)
                        continue;

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < 0)
            {
                EditorUtility.DisplayDialog(
                    "Boş resim",
                    "Görünür piksel bulunamadı. Bölüm değiştirilmedi.",
                    "Tamam");

                return;
            }

            int croppedWidth = maxX - minX + 1;
            int croppedHeight = maxY - minY + 1;

            Color32[] palette = new Color32[level.Palette.Count];

            for (int i = 0; i < palette.Length; i++)
            {
                palette[i] = level.Palette.GetColor(i);
            }

            int[] cells = new int[croppedWidth * croppedHeight];
            int filledCount = 0;

            // Kırpılmış alanı doğrudan bölüm verisine dönüştür.
            for (int y = 0; y < croppedHeight; y++)
            {
                for (int x = 0; x < croppedWidth; x++)
                {
                    int sourceIndex =
                        (minY + y) * texture.width + minX + x;

                    int cellIndex = y * croppedWidth + x;
                    Color32 pixel = sourcePixels[sourceIndex];

                    if (pixel.a < AlphaThreshold)
                    {
                        cells[cellIndex] = LevelData.EmptyCell;
                        continue;
                    }

                    cells[cellIndex] = FindNearestColor(pixel, palette);
                    filledCount++;
                }
            }

            bool accepted = EditorUtility.DisplayDialog(
                "PNG'den bölüm oluştur",
                $"Orijinal: {texture.width} × {texture.height}\n" +
                $"Kırpılmış: {croppedWidth} × {croppedHeight}\n" +
                $"Küp sayısı: {filledCount}\n\n" +
                "Mevcut tahta değiştirilecek ve shooter listesi " +
                "temizlenecek. İşlem geri alınabilir.",
                "İçe Aktar",
                "Vazgeç");

            if (!accepted)
                return;

            using (SerializedObject data = new SerializedObject(level))
            {
                data.Update();

                data.FindProperty("width").intValue = croppedWidth;
                data.FindProperty("height").intValue = croppedHeight;

                SerializedProperty targetCells =
                    data.FindProperty("cells");

                targetCells.arraySize = cells.Length;

                for (int i = 0; i < cells.Length; i++)
                {
                    targetCells.GetArrayElementAtIndex(i).intValue =
                        cells[i];
                }

                data.FindProperty("shooters").arraySize = 0;

                data.ApplyModifiedProperties();
            }

            Debug.Log(
                $"PNG içe aktarıldı: {croppedWidth} × {croppedHeight}, " +
                $"{filledCount} küp. Bölüm editöründen Kaydet'e bas.",
                level);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "İçe aktarma hatası",
                exception.Message,
                "Tamam");
        }
        finally
        {
            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }

    private static int FindNearestColor(
        Color32 pixel, Color32[] palette)
    {
        int bestIndex = 0;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < palette.Length; i++)
        {
            int r = pixel.r - palette[i].r;
            int g = pixel.g - palette[i].g;
            int b = pixel.b - palette[i].b;

            int distance = r * r + g * g + b * b;

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestIndex = i;
        }

        return bestIndex;
    }
}