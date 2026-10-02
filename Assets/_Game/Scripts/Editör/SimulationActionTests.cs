using System;
using UnityEditor;
using UnityEngine;

public static class SimulationActionTests
{
    [MenuItem("Tools/ColorLoop/Tests/Simülasyon Hamleleri")]
    private static void Run()
    {
        try
        {
            var board = new BoardState(
                1,
                1,
                new[] { 0 });

            var shooters = new[]
            {
                new ShooterState(3, 2),
                new ShooterState(0, 1)
            };

            var state = new SimulationState(
                board,
                shooters,
                2,
                2);

            var simulator = new GameSimulator(
                state,
                0.15d,
                0.7d,
                0.6d,
                1.5d);

            int[] results = new int[state.WaitingSlotCount + 1];

            // Başlangıçta yalnızca kuyruğun başı gönderilebilir.
            int count = simulator.GetLaunchableShooters(results);

            Require(count == 1 && results[0] == 0,
                "Başlangıçta yalnızca ilk shooter gönderilebilmeliydi.");

            Require(state.NextQueueIndex == 0 &&
                    state.ActiveShooterCount == 0,
                "Hamleleri listelemek oyun durumunu değiştirdi.");

            // Kopyada yapılan hamle asıl simülasyonu etkilememeli.
            GameSimulator branch = simulator.Clone();

            Require(
                branch.TryLaunch(0) == LaunchResult.Success,
                "Kopyada shooter gönderilemedi.");

            Require(
                simulator.State.ActiveShooterCount == 0 &&
                simulator.State.NextQueueIndex == 0,
                "Kopyadaki hamle asıl simülasyonu değiştirdi.");

            // Asıl simülasyonda ilk shooter'ı gönder.
            Require(
                simulator.TryLaunch(0) == LaunchResult.Success,
                "İlk shooter gönderilemedi.");

            count = simulator.GetLaunchableShooters(results);

            Require(count == 0,
                "Giriş doluyken başka shooter gönderilebilir görünüyor.");

            // Yeşil shooter kırmızı bloğu silemez.
            // Tur sonunda bekleme alanına geçmeli.
            double lapTime =
                simulator.PathLength / simulator.MovementSpeed;

            simulator.Advance(lapTime + 0.1d);

            count = simulator.GetLaunchableShooters(results);

            Require(count == 2,
                "Kuyruk başı ve bekleyen shooter seçenekleri bulunamadı.");

            Require(results[0] == 1 && results[1] == 0,
                "Beklenen hamle listesi: kuyruktaki 1, bekleyen 0.");

            // İki farklı kararı ayrı kopyalarda dene.
            GameSimulator queueBranch = simulator.Clone();
            GameSimulator waitingBranch = simulator.Clone();

            Require(
                queueBranch.TryLaunch(1) == LaunchResult.Success,
                "Kuyruk hamlesi uygulanamadı.");

            Require(
                waitingBranch.TryLaunch(0) == LaunchResult.Success,
                "Bekleme alanı hamlesi uygulanamadı.");

            Require(
                simulator.State.GetWaitingOccupant(0) == 0 &&
                simulator.State.NextQueueIndex == 1 &&
                simulator.State.ActiveShooterCount == 0,
                "Hamle denemeleri kaynak simülasyonu değiştirdi.");

            // Kırmızı shooter kalan kırmızı bloğu temizlemeli.
            queueBranch.Advance(10d);

            Require(
                queueBranch.State.Status == SimulationStatus.Won,
                "Kırmızı shooter'ın seçildiği dal kazanamadı.");

            Require(
                simulator.State.Board.RemainingBlocks == 1,
                "Çözülen dal kaynak tahtayı değiştirdi.");

            Debug.Log(
                "Hamle testleri geçti: geçerli seçenekler, " +
                "giriş engeli ve bağımsız karar dalları.");

            EditorUtility.DisplayDialog(
                "Test geçti",
                "Hamle listesi ve bağımsız karar dalları doğru çalıştı.",
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