using System;
using UnityEditor;
using UnityEngine;

public static class LevelSimulationFactoryTests
{
    [MenuItem("Tools/ColorLoop/Tests/Seçili Bölümü Simülasyona Aktar")]
    private static void Run()
    {
        LevelData level = Selection.activeObject as LevelData;

        if (level == null)
        {
            EditorUtility.DisplayDialog(
                "Bölüm seç",
                "Önce Project penceresinden bir LevelData dosyası seç.",
                "Tamam");

            return;
        }

        try
        {
            // Bu testin örnek ayarları.
            // Solver penceresinde bunları düzenlenebilir yapacağız.
            var simulator = LevelSimulationFactory.Create(
                level,
                conveyorCapacity: 2,
                waitingSlotCount: 5,
                cellSize: 0.15d,
                distanceFromBoard: 0.7d,
                entryClearance: 0.6d,
                movementSpeed: 1.5d);

            SimulationState state = simulator.State;

            Require(
                state.Board.Width == level.Width &&
                state.Board.Height == level.Height,
                "Tahta boyutları doğru aktarılmadı.");

            Require(
                state.ShooterCount == level.ShooterCount,
                "Shooter sayısı doğru aktarılmadı.");

            int blockCount = 0;
            int firstX = -1;
            int firstY = -1;
            int firstColor = BoardState.EmptyCell;

            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    int colorId = level.GetCell(x, y);

                    Require(
                        state.Board.GetColor(x, y) == colorId,
                        $"Hücre doğru aktarılmadı: ({x}, {y}).");

                    if (colorId == BoardState.EmptyCell)
                        continue;

                    blockCount++;

                    if (firstX >= 0)
                        continue;

                    firstX = x;
                    firstY = y;
                    firstColor = colorId;
                }
            }

            Require(
                state.Board.RemainingBlocks == blockCount,
                "Kalan blok sayısı doğru hesaplanmadı.");

            for (int i = 0; i < level.ShooterCount; i++)
            {
                ShooterData source = level.GetShooter(i);
                ShooterState copy = state.GetShooter(i).State;

                Require(
                    copy.ColorId == source.ColorId &&
                    copy.RemainingAmmo == source.Ammo,
                    $"Shooter {i} doğru aktarılmadı.");
            }

            // Simülasyonu değiştirmek kaynak bölümü etkilememeli.
            if (firstX >= 0)
            {
                bool removed = state.Board.TryRemoveBlock(
                    firstX,
                    firstY,
                    firstColor,
                    out _);

                Require(removed,
                    "Simülasyon kopyasındaki hücre silinemedi.");

                Require(
                    level.GetCell(firstX, firstY) == firstColor,
                    "Simülasyondaki işlem kaynak bölümü değiştirdi.");
            }

            if (state.ShooterCount > 0)
            {
                int originalAmmo = level.GetShooter(0).Ammo;

                state.GetShooter(0).State.TryConsumeAmmo();

                Require(
                    level.GetShooter(0).Ammo == originalAmmo,
                    "Simülasyondaki mühimmat kaynak bölümü değiştirdi.");

                Require(
                    state.GetShooter(0).State.RemainingAmmo ==
                    originalAmmo - 1,
                    "Simülasyon mühimmatı azalmadı.");
            }

            string report =
                $"Bölüm: {level.name}\n" +
                $"Boyut: {level.Width} × {level.Height}\n" +
                $"Blok: {blockCount}\n" +
                $"Shooter: {level.ShooterCount}\n\n" +
                "Veriler doğru aktarıldı.\n" +
                "Kaynak bölüm değiştirilmedi.\n\n" +
                "Bu test çözülebilirlik kontrolü değildir.";

            Debug.Log(report);

            EditorUtility.DisplayDialog(
                "Aktarım testi geçti",
                report,
                "Tamam");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Aktarım testi başarısız",
                exception.Message,
                "Tamam");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}