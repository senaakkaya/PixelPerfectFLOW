using UnityEditor;
using UnityEngine;

public static class LevelCropper
{
    public static void Crop(LevelData level)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (level == null || !level.HasBoardData)
        {
            EditorUtility.DisplayDialog(
                "Kırpma yapılamadı",
                "Önce geçerli bir bölüm seç.",
                "Tamam");
            return;
        }

        int minX = level.Width;
        int minY = level.Height;
        int maxX = -1;
        int maxY = -1;

        // Dolu hücrelerin kapladığı sınırları bul.
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                if (level.GetCell(x, y) == LevelData.EmptyCell)
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
                "Boş tahta",
                "Tahtada dolu hücre yok. Bölüm değiştirilmedi.",
                "Tamam");
            return;
        }

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;

        if (width == level.Width && height == level.Height)
        {
            EditorUtility.DisplayDialog(
                "Kırpma gerekmiyor",
                "Dış kenarlarda tamamen boş satır veya sütun yok.",
                "Tamam");
            return;
        }

        bool accepted = EditorUtility.DisplayDialog(
            "Boş kenarları kırp",
            $"Eski boyut: {level.Width} × {level.Height}\n" +
            $"Yeni boyut: {width} × {height}\n\n" +
            "Dolu hücreler ve iç boşluklar korunacak. " +
            "Shooter listesi temizlenecek. İşlem geri alınabilir.",
            "Kırp",
            "Vazgeç");

        if (!accepted)
            return;

        // Kaynak veriyi değiştirmeden önce yeni alanı kopyala.
        int[] croppedCells = new int[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                croppedCells[y * width + x] =
                    level.GetCell(minX + x, minY + y);
            }
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Boş Kenarları Kırp");

        using (SerializedObject data = new SerializedObject(level))
        {
            data.Update();

            data.FindProperty("width").intValue = width;
            data.FindProperty("height").intValue = height;

            SerializedProperty cells = data.FindProperty("cells");
            cells.arraySize = croppedCells.Length;

            for (int i = 0; i < croppedCells.Length; i++)
            {
                cells.GetArrayElementAtIndex(i).intValue =
                    croppedCells[i];
            }

            data.FindProperty("shooters").arraySize = 0;

            data.ApplyModifiedProperties();
        }

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log(
            $"Tahta kırpıldı: {width} × {height}. " +
            "Bölüm editöründen Kaydet'e bas.",
            level);
    }
}