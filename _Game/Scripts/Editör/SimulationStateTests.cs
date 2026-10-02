using System;
using UnityEditor;
using UnityEngine;

public static class SimulationStateTests
{
    [MenuItem("Tools/ColorLoop/Tests/Simulation Kopyasını Kontrol Et")]
    private static void TestClone()
    {
        try
        {
            // Alt satır: yeşil, kırmızı.
            // Üst satır: boş, yeşil.
            int[] cells =
            {
                3, 0,
                BoardState.EmptyCell, 3
            };

            BoardState initialBoard = new BoardState(2, 2, cells);

            ShooterState[] initialShooters =
            {
                new ShooterState(3, 2),
                new ShooterState(0, 1)
            };

            SimulationState original = new SimulationState(
                initialBoard,
                initialShooters,
                conveyorCapacity: 2,
                waitingSlotCount: 5);

            SimulationState copy = original.Clone();

            Require(
                copy.Board.TryRemoveBlock(0, 0, 3, out _),
                "Kopyadaki yeşil blok kaldırılamadı.");

            Require(
                copy.GetShooter(0).State.TryConsumeAmmo(),
                "Kopyadaki shooter mühimmat harcayamadı.");

            Require(
                original.Board.GetColor(0, 0) == 3,
                "Asıl tahtadaki hücre değişti.");

            Require(
                original.Board.RemainingBlocks == 3,
                "Asıl tahtanın blok sayısı değişti.");

            Require(
                copy.Board.RemainingBlocks == 2,
                "Kopyanın blok sayısı yanlış.");

            Require(
                original.GetShooter(0).State.RemainingAmmo == 2,
                "Asıl shooter'ın mühimmatı değişti.");

            Require(
                copy.GetShooter(0).State.RemainingAmmo == 1,
                "Kopyadaki mühimmat yanlış.");

            Require(
                initialBoard.RemainingBlocks == 3 &&
                initialShooters[0].RemainingAmmo == 2,
                "Kurucuya verilen kaynak nesneler değişti.");

            Require(
                copy.NextQueueIndex == original.NextQueueIndex &&
                copy.Time == original.Time &&
                copy.Status == original.Status &&
                copy.ConveyorCapacity == original.ConveyorCapacity,
                "Ortak durum bilgileri kopyalanamadı.");

            Require(
                copy.ActiveShooterCount == 0 &&
                copy.GetShooter(0).Location == ShooterLocation.Queue,
                "Başlangıç shooter konumu yanlış.");

            for (int i = 0; i < copy.WaitingSlotCount; i++)
            {
                Require(
                    copy.GetWaitingOccupant(i) == -1,
                    "Başlangıçta dolu bekleme yuvası bulundu.");
            }

            Debug.Log(
                "SimulationState testi geçti: " +
                "tahta ve mühimmat kopyaları bağımsız, " +
                "başlangıç durumu doğru.");

            EditorUtility.DisplayDialog(
                "Test geçti",
                "Kopyadaki değişiklikler asıl oyun verisini etkilemedi.",
                "Tamam");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Test başarısız",
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