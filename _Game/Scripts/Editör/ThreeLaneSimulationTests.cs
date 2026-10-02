using System;
using UnityEditor;
using UnityEngine;

public static class ThreeLaneSimulationTests
{
    [MenuItem("Tools/ColorLoop/Tests/Üç Sıralı Simülasyon")]
    private static void Run()
    {
        try
        {
            TestIndependentLanes();
            TestSolverAndReplay();

            Debug.Log(
                "Üç sıra testleri geçti: ön seçimler, bağımsız " +
                "sıra ilerlemesi, kopyalama ve solver çözümü.");

            EditorUtility.DisplayDialog(
                "Test geçti",
                "Üç sıralı simülasyon doğru çalıştı.",
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

    private static void TestIndependentLanes()
    {
        GameSimulator source = CreateSimulator();
        SimulationState state = source.State;

        int[] choices =
            new int[state.QueueLaneCount + state.WaitingSlotCount];

        int count = source.GetLaunchableShooters(choices);

        Require(count == 3,
            "Başlangıçta üç ön shooter seçilebilir olmalı.");

        Require(
            choices[0] == 0 &&
            choices[1] == 1 &&
            choices[2] == 2,
            "Sıra başları yanlış.");

        Require(
            source.CanLaunch(4) == LaunchResult.NotQueueFront,
            "Orta sıranın arkasındaki shooter seçilebilir olmamalı.");

        GameSimulator branch = source.Clone();

        Require(
            branch.TryLaunch(1) == LaunchResult.Success,
            "Orta sıranın başındaki shooter gönderilemedi.");

        Require(
            branch.State.GetQueueHead(0) == 0 &&
            branch.State.GetQueueHead(1) == 4 &&
            branch.State.GetQueueHead(2) == 2,
            "Yalnızca orta sıra ilerlemeliydi.");

        Require(
            source.State.GetQueueHead(1) == 1,
            "Kopyadaki hamle kaynak sırayı değiştirdi.");

        Require(
            branch.CanLaunch(4) == LaunchResult.EntryBlocked,
            "Sıradaki shooter giriş boşalmadan gönderilebilir görünüyor.");
    }

    private static void TestSolverAndReplay()
    {
        GameSimulator source = CreateSimulator();

        SolverResult result = LevelSolver.Solve(
            source,
            new SolverSettings
            {
                MaxVisitedStates = 5000,
                MaxDepth = 64,
                TimeLimitMilliseconds = 5000,
                WaitSeconds = 0.25d
            });

        Require(result.Status == SolverStatus.Solved,
            "Üç sıralı basit bölüm çözülemedi.");

        GameSimulator replay = source.Clone();

        foreach (SolverAction action in result.Actions)
        {
            if (action.Type == SolverActionType.Launch)
            {
                Require(
                    replay.TryLaunch(action.ShooterIndex) ==
                    LaunchResult.Success,
                    "Çözümde geçersiz gönderme hamlesi var.");
            }
            else
            {
                replay.Advance(action.Seconds);
            }
        }

        Require(replay.State.Status == SimulationStatus.Won,
            "Bulunan çözüm tekrar oynatıldığında kazanmadı.");

        Require(
            source.State.Board.RemainingBlocks == 1 &&
            source.State.GetQueueHead(0) == 0 &&
            source.State.GetQueueHead(1) == 1 &&
            source.State.GetQueueHead(2) == 2,
            "Solver kaynak durumu değiştirdi.");
    }

    private static GameSimulator CreateSimulator()
    {
        var shooters = new[]
        {
            new ShooterState(3, 1),
            new ShooterState(0, 1),
            new ShooterState(1, 1),
            new ShooterState(3, 1),
            new ShooterState(0, 1),
            new ShooterState(1, 1)
        };

        var state = new SimulationState(
            new BoardState(1, 1, new[] { 0 }),
            shooters,
            4,
            5,
            new[] { 0, 1, 2, 0, 1, 2 });

        return new GameSimulator(
            state,
            0.15d,
            0.7d,
            0.6d,
            1.5d);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}