using System.Text;
using UnityEditor;
using UnityEngine;

public static class LevelValidator
{
    public static void Validate(LevelData level)
    {
        if (level == null ||
            !level.HasBoardData ||
            level.Palette == null ||
            level.Palette.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Bölüm kontrolü",
                "Tahta verisi veya renk paleti eksik.",
                "Tamam");
            return;
        }

        int colorCount = level.Palette.Count;

        int[] blockCounts = new int[colorCount];
        long[] ammoCounts = new long[colorCount];

        int totalBlocks = 0;

        // Tahtadaki renkleri say.
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                int colorId = level.GetCell(x, y);

                if (colorId == LevelData.EmptyCell)
                    continue;

                if (!level.Palette.Contains(colorId))
                {
                    EditorUtility.DisplayDialog(
                        "Geçersiz hücre",
                        $"({x}, {y}) hücresinde geçersiz renk: {colorId}",
                        "Tamam");
                    return;
                }

                blockCounts[colorId]++;
                totalBlocks++;
            }
        }

        if (totalBlocks == 0)
        {
            EditorUtility.DisplayDialog(
                "Boş bölüm",
                "Tahtada kırılacak küp bulunmuyor.",
                "Tamam");
            return;
        }

        // Shooter mühimmatlarını renklerine göre topla.
        for (int i = 0; i < level.ShooterCount; i++)
        {
            ShooterData shooter = level.GetShooter(i);

            if (!level.Palette.Contains(shooter.ColorId) ||
                shooter.Ammo <= 0)
            {
                EditorUtility.DisplayDialog(
                    "Geçersiz shooter",
                    $"Element {i}: Renk kimliğini ve mühimmatı kontrol et.",
                    "Tamam");
                return;
            }

            ammoCounts[shooter.ColorId] += shooter.Ammo;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine($"Toplam küp: {totalBlocks}");
        report.AppendLine($"Shooter sayısı: {level.ShooterCount}");
        report.AppendLine();

        bool hasShortage = false;

        for (int colorId = 0; colorId < colorCount; colorId++)
        {
            int blocks = blockCounts[colorId];
            long ammo = ammoCounts[colorId];

            if (blocks == 0 && ammo == 0)
                continue;

            long difference = ammo - blocks;

            string colorHex = ColorUtility.ToHtmlStringRGB(
                level.Palette.GetColor(colorId));

            report.AppendLine($"Renk {colorId} — #{colorHex}");
            report.AppendLine($"Küp: {blocks} | Mühimmat: {ammo}");

            if (difference < 0)
            {
                hasShortage = true;
                report.AppendLine($"EKSİK: {-difference} mermi");
            }
            else if (difference > 0)
            {
                report.AppendLine($"FAZLA: {difference} mermi");
            }
            else
            {
                report.AppendLine("Miktar tam.");
            }

            report.AppendLine();
        }

        if (hasShortage)
        {
            report.AppendLine(
                "Sonuç: Mevcut kurallarla mühimmat yetersiz.");
        }
        else
        {
            report.AppendLine(
                "Sonuç: Her renk için yeterli mühimmat var.");

            report.AppendLine(
                "Bu sonuç, bölümün çözülebilir olduğunu garanti etmez.");
        }

        string reportText = report.ToString();

        Debug.Log(reportText, level);

        EditorUtility.DisplayDialog(
            "Bölüm Kontrol Raporu",
            reportText,
            "Tamam");
    }
}