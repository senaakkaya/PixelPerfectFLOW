using System;
using UnityEditor;
using UnityEngine;

public static class WaitingReturnPuzzleTests
{
    [MenuItem("Tools/ColorLoop/Tests/Beklemeye Dönüş Bulmacası")]
    private static void Run()
    {
        try
        {
            TestRequiredReturn();
            TestSolverFindsReturn();

            Debug.Log(
                "Beklemeye dönüş testi geçti: yeşil 1 mermiyle " +
                "bekledi, kırmızı yolu açtı, yeşil tekrar gönderildi. " +
                "Solver da bu çözümü bulup tekrar oynattı.");

            EditorUtility.DisplayDialog(
                "Test geçti",
                "Bekleyen shooter'ın tekrar gönderilmesini gerektiren " +
                "bulmaca çözüldü ve doğrulandı.",
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

    private static void TestRequiredReturn()
    {
        GameSimulator simulator = CreatePuzzle();

        double lapDuration =
            simulator.PathLength / simulator.MovementSpeed
            + 0.000001d;

        Require(
            simulator.TryLaunch(0) == LaunchResult.Success,
            "Yeşil shooter gönderilemedi.");

        Require(
            simulator.CanLaunch(1) == LaunchResult.ConveyorFull,
            "Kapasite 1 iken kırmızı da banda girebiliyor.");

        simulator.Advance(lapDuration);

        var green = simulator.State.GetShooter(0);

        Require(
            green.Location == ShooterLocation.Waiting,
            "Yeşil shooter tur sonunda beklemeye dönmedi.");

        Require(
            green.State.RemainingAmmo == 1,
            "Yeşilin 1 mermisi kalmalıydı.");

        Require(
            simulator.State.GetWaitingOccupant(0) == 0,
            "Yeşil bekleme slotuna kaydedilmedi.");

        Require(
            simulator.State.Board.RemainingBlocks == 8,
            "İlk turda yalnızca köşedeki yeşil silinmeliydi.");

        Require(
            simulator.State.Board.GetColor(1, 1) == 3,
            "Ortadaki yeşil henüz silinmemeliydi.");

        Require(
            simulator.TryLaunch(1) == LaunchResult.Success,
            "Kırmızı shooter gönderilemedi.");

        simulator.Advance(lapDuration);

        Require(
            simulator.State.Board.RemainingBlocks == 1,
            "Kırmızıdan sonra yalnızca ortadaki yeşil kalmalıydı.");

        Require(
            simulator.State.Status == SimulationStatus.Running,
            "Bekleyen yeşil varken oyun bitmiş sayıldı.");

        Require(
            simulator.TryLaunch(0) == LaunchResult.Success,
            "Bekleyen yeşil tekrar gönderilemedi.");

        Require(
            simulator.State.GetWaitingOccupant(0) == -1,
            "Yeşil gönderilince bekleme slotu boşalmadı.");

        simulator.Advance(lapDuration);

        Require(
            simulator.State.Status == SimulationStatus.Won,
            "Yeşil tekrar gönderildikten sonra bölüm tamamlanmadı.");

        Require(
            simulator.State.GetShooter(0).State.RemainingAmmo == 0,
            "Yeşilin son mermisi tüketilmedi.");
    }

    private static void TestSolverFindsReturn()
    {
        GameSimulator source = CreatePuzzle();

        SolverResult result = LevelSolver.Solve(
            source,
            new SolverSettings
            {
                MaxVisitedStates = 10000,
                MaxDepth = 128,
                TimeLimitMilliseconds = 5000,
                WaitSeconds = 0.25d
            });

        Require(
            result.Status == SolverStatus.Solved,
            "Solver beklemeye dönüş bulmacasında çözüm bulamadı.");

        GameSimulator replay = source.Clone();

        int greenLaunchCount = 0;
        bool relaunchedFromWaiting = false;

        foreach (SolverAction action in result.Actions)
        {
            if (action.Type == SolverActionType.Wait)
            {
                replay.Advance(action.Seconds);
                continue;
            }

            var shooter = replay.State.GetShooter(
                action.ShooterIndex);

            bool wasWaiting =
                shooter.Location == ShooterLocation.Waiting;

            Require(
                replay.TryLaunch(action.ShooterIndex) ==
                LaunchResult.Success,
                "Solver çözümünde geçersiz gönderme hamlesi var.");

            if (action.ShooterIndex == 0)
            {
                greenLaunchCount++;

                if (wasWaiting)
                    relaunchedFromWaiting = true;
            }
        }

        Require(
            replay.State.Status == SimulationStatus.Won,
            "Solver çözümü tekrar oynatılınca kazanmadı.");

        Require(
            greenLaunchCount >= 2 && relaunchedFromWaiting,
            "Çözümde bekleyen yeşil tekrar gönderilmedi.");

        Require(
            source.State.Board.RemainingBlocks == 9 &&
            source.State.GetQueueHead(0) == 0 &&
            source.State.GetWaitingOccupant(0) == -1,
            "Solver kaynak durumu değiştirdi.");
    }

    private static GameSimulator CreatePuzzle()
    {
        // İlk üç değer tahtanın alt satırıdır.
        var board = new BoardState(
            3,
            3,
            new[]
            {
                3, 0, 0,
                0, 3, 0,
                0, 0, 0
            });

        var shooters = new[]
        {
            new ShooterState(3, 2),
            new ShooterState(0, 7)
        };

        var state = new SimulationState(
            board,
            shooters,
            conveyorCapacity: 1,
            waitingSlotCount: 1,
            laneIndices: new[] { 0, 0 });

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