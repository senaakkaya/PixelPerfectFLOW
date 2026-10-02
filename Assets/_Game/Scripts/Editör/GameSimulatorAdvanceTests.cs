using System;
using UnityEditor;
using UnityEngine;

public static class GameSimulatorAdvanceTests
{
    [MenuItem("Tools/ColorLoop/Tests/Simülasyon Hareket ve Atış")]
    private static void Run()
    {
        try
        {
            TestWin();
            TestAmmoDepletion();
            TestWrongColorBlocksShot();
            TestWaitingAndRelaunch();
            TestWaitingOverflow();
            TestTimeStepConsistency();

            Debug.Log(
                "Simülasyon testleri geçti: kazanma, mühimmat, " +
                "renk engeli, bekleme, tekrar gönderme, " +
                "bekleme taşması ve zaman adımı tutarlılığı.");

            EditorUtility.DisplayDialog(
                "Test geçti",
                "Hareket ve atış testleri başarıyla tamamlandı.",
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

    private static void TestWin()
    {
        var state = CreateState(
            1, 1,
            new[] { 3 },
            new[] { new ShooterState(3, 1) });

        var simulator = CreateSimulator(state);

        Require(
            simulator.TryLaunch(0) == LaunchResult.Success,
            "Kazanma testi: shooter gönderilemedi.");

        simulator.Advance(10d);

        Require(state.Board.RemainingBlocks == 0,
            "Son blok silinmedi.");

        Require(state.GetShooter(0).State.RemainingAmmo == 0,
            "Son atışın mühimmatı tüketilmedi.");

        Require(state.Status == SimulationStatus.Won,
            "Kazanma durumu oluşmadı.");
    }

    private static void TestAmmoDepletion()
    {
        var state = CreateState(
            2, 1,
            new[] { 3, 3 },
            new[] { new ShooterState(3, 1) });

        var simulator = CreateSimulator(state);

        simulator.TryLaunch(0);
        simulator.Advance(10d);

        Require(state.Board.RemainingBlocks == 1,
            "Bir mermiyle tam olarak bir blok silinmeliydi.");

        Require(
            state.GetShooter(0).Location == ShooterLocation.Finished,
            "Mühimmatı biten shooter banttan ayrılmadı.");

        Require(state.ActiveShooterCount == 0,
            "Bitmiş shooter bant kapasitesini işgal ediyor.");
    }

    private static void TestWrongColorBlocksShot()
    {
        // Alt hücre kırmızı; arkasındaki hücre yeşil.
        var state = CreateState(
            1, 2,
            new[] { 0, 3 },
            new[] { new ShooterState(3, 2) });

        var simulator = CreateSimulator(state);

        simulator.TryLaunch(0);

        // İlk alt-kenar atışı gerçekleşti, sağ kenara henüz ulaşılmadı.
        simulator.Advance(0.6d);

        Require(state.Board.RemainingBlocks == 2,
            "Shooter farklı renkteki ön bloğun arkasına ateş etti.");

        Require(state.GetShooter(0).State.RemainingAmmo == 2,
            "Renk eşleşmediği halde mühimmat azaldı.");
    }

    private static void TestWaitingAndRelaunch()
    {
        var state = CreateState(
            1, 1,
            new[] { 0 },
            new[] { new ShooterState(3, 2) });

        var simulator = CreateSimulator(state);

        simulator.TryLaunch(0);
        simulator.Advance(
            simulator.PathLength / simulator.MovementSpeed + 0.1d);

        Require(
            state.GetShooter(0).Location == ShooterLocation.Waiting,
            "Tur bitince shooter bekleme alanına geçmedi.");

        Require(state.GetWaitingOccupant(0) == 0,
            "Bekleme slotuna shooter kaydedilmedi.");

        Require(
            simulator.TryLaunch(0) == LaunchResult.Success,
            "Bekleyen shooter tekrar gönderilemedi.");

        Require(state.GetWaitingOccupant(0) == -1,
            "Tekrar gönderilince bekleme slotu boşalmadı.");

        Require(state.NextQueueIndex == 1,
            "Beklemeden gönderme kuyruk indeksini değiştirdi.");
    }

    private static void TestWaitingOverflow()
    {
        var state = CreateState(
            1, 1,
            new[] { 0 },
            new[]
            {
                new ShooterState(3, 2),
                new ShooterState(3, 2)
            },
            waitingSlots: 1);

        var simulator = CreateSimulator(state);

        double lapTime =
            simulator.PathLength / simulator.MovementSpeed + 0.1d;

        simulator.TryLaunch(0);
        simulator.Advance(lapTime);

        Require(state.Status == SimulationStatus.Running,
            "Son boş slota yerleşmek tek başına kaybettirmemeli.");

        Require(
            simulator.TryLaunch(1) == LaunchResult.Success,
            "İkinci shooter gönderilemedi.");

        simulator.Advance(lapTime);

        Require(state.Status == SimulationStatus.Lost,
            "Yer bulamayan shooter kaybetme durumunu oluşturmadı.");
    }

    private static void TestTimeStepConsistency()
    {
        var initial = CreateState(
            3, 3,
            new[]
            {
                3, 3, 3,
                3, 0, 3,
                3, 3, 3
            },
            new[]
            {
                new ShooterState(3, 20),
                new ShooterState(0, 20)
            });

        var largeStep = CreateSimulator(initial.Clone());
        var smallSteps = CreateSimulator(initial.Clone());

        Require(
            largeStep.TryLaunch(0) == LaunchResult.Success &&
            smallSteps.TryLaunch(0) == LaunchResult.Success,
            "Zaman testi: ilk shooter gönderilemedi.");

        largeStep.Advance(0.5d);
        smallSteps.Advance(0.5d);

        Require(
            largeStep.TryLaunch(1) == LaunchResult.Success &&
            smallSteps.TryLaunch(1) == LaunchResult.Success,
            "Zaman testi: ikinci shooter gönderilemedi.");

        largeStep.Advance(8d);

        // 0.125 ikilik sistemde tam temsil edilir.
        for (int i = 0; i < 64; i++)
            smallSteps.Advance(0.125d);

        AssertEquivalent(largeStep.State, smallSteps.State);
    }

    private static void AssertEquivalent(
        SimulationState a,
        SimulationState b)
    {
        Require(a.Status == b.Status,
            "Zaman adımı değişince oyun sonucu değişti.");

        Require(a.Board.RemainingBlocks == b.Board.RemainingBlocks,
            "Zaman adımı değişince kalan blok sayısı değişti.");

        Require(Math.Abs(a.Time - b.Time) < 0.000001d,
            "Simülasyon saatleri farklı.");

        for (int y = 0; y < a.Board.Height; y++)
        {
            for (int x = 0; x < a.Board.Width; x++)
            {
                Require(
                    a.Board.GetColor(x, y) == b.Board.GetColor(x, y),
                    $"Hücre sonuçları farklı: ({x}, {y}).");
            }
        }

        for (int i = 0; i < a.ShooterCount; i++)
        {
            var first = a.GetShooter(i);
            var second = b.GetShooter(i);

            Require(first.Location == second.Location,
                $"Shooter {i}: konum durumu farklı.");

            Require(
                first.State.RemainingAmmo == second.State.RemainingAmmo,
                $"Shooter {i}: mühimmat farklı.");

            Require(first.NextShotPoint == second.NextShotPoint,
                $"Shooter {i}: atış noktası farklı.");

            Require(Math.Abs(first.Distance - second.Distance) < 0.000001d,
                $"Shooter {i}: yol mesafesi farklı.");
        }

        for (int i = 0; i < a.WaitingSlotCount; i++)
        {
            Require(
                a.GetWaitingOccupant(i) == b.GetWaitingOccupant(i),
                $"Bekleme slotu {i}: sonuç farklı.");
        }
    }

    private static SimulationState CreateState(
        int width,
        int height,
        int[] cells,
        ShooterState[] shooters,
        int waitingSlots = 2)
    {
        return new SimulationState(
            new BoardState(width, height, cells),
            shooters,
            2,
            waitingSlots);
    }

    private static GameSimulator CreateSimulator(SimulationState state)
    {
        return new GameSimulator(
            state,
            cellSize: 0.15d,
            distanceFromBoard: 0.7d,
            entryClearance: 0.6d,
            movementSpeed: 1.5d);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}