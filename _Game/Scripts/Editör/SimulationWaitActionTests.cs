using System;
using UnityEditor;
using UnityEngine;

public static class SimulationWaitActionTests
{
    [MenuItem("Tools/ColorLoop/Tests/Simülasyon Bekleme Hamlesi")]
    private static void Run()
    {
        try
        {
            var state = new SimulationState(
                new BoardState(1, 1, new[] { 0 }),
                new[]
                {
                    new ShooterState(3, 2),
                    new ShooterState(0, 1)
                },
                2,
                2);

            var simulator = new GameSimulator(
                state,
                cellSize: 0.15d,
                distanceFromBoard: 0.7d,
                entryClearance: 0.6d,
                movementSpeed: 1.5d);

            // Bant boşken beklemek gereksiz bir hamle.
            bool created = SimulationWaitAction.TryCreateBranch(
                simulator,
                0.5d,
                out GameSimulator emptyBranch);

            Require(!created && emptyBranch == null,
                "Bant boşken gereksiz bekleme dalı oluşturuldu.");

            Require(
                simulator.TryLaunch(0) == LaunchResult.Success,
                "İlk shooter gönderilemedi.");

            Require(
                simulator.CanLaunch(1) == LaunchResult.EntryBlocked,
                "Başlangıçta bant girişinin dolu olması gerekiyordu.");

            // 0.5 saniyede 0.75 birim ilerler: giriş açılmalı.
            created = SimulationWaitAction.TryCreateBranch(
                simulator,
                0.5d,
                out GameSimulator waited);

            Require(created && waited != null,
                "Bekleme dalı oluşturulamadı.");

            Require(
                waited.CanLaunch(1) == LaunchResult.Success,
                "Bekledikten sonra bant girişi açılmadı.");

            Require(
                simulator.CanLaunch(1) == LaunchResult.EntryBlocked,
                "Bekleme dalı kaynak simülasyonu değiştirdi.");

            Require(simulator.State.Time == 0d,
                "Kaynak simülasyonun saati değişti.");

            Require(Math.Abs(waited.State.Time - 0.5d) < 0.000001d,
                "Bekleme süresi doğru uygulanmadı.");

            // Bekleme dalında ikinci shooter'ı gönder.
            Require(
                waited.TryLaunch(1) == LaunchResult.Success,
                "Giriş açılmasına rağmen ikinci shooter gönderilemedi.");

            // Bir bekleme hamlesi oyunu da bitirebilir.
            created = SimulationWaitAction.TryCreateBranch(
                waited,
                10d,
                out GameSimulator completed);

            Require(created,
                "İkinci bekleme dalı oluşturulamadı.");

            Require(
                completed.State.Status == SimulationStatus.Won,
                "Kırmızı shooter bloğu temizleyip kazanamadı.");

            Require(
                waited.State.Status == SimulationStatus.Running &&
                waited.State.Board.RemainingBlocks == 1,
                "Tamamlanan dal kaynak durumu değiştirdi.");

            // Oyun bittikten sonra bekleme hamlesi üretilmemeli.
            created = SimulationWaitAction.TryCreateBranch(
                completed,
                0.5d,
                out GameSimulator endedBranch);

            Require(!created && endedBranch == null,
                "Bitmiş oyun için bekleme dalı oluşturuldu.");

            Debug.Log(
                "Bekleme testleri geçti: girişin açılması, " +
                "kopya bağımsızlığı, kazanma ve gereksiz bekleme kontrolü.");

            EditorUtility.DisplayDialog(
                "Test geçti",
                "Bekleme hamlesi doğru çalıştı.",
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