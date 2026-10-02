using System;
using UnityEditor;
using UnityEngine;

public static class LevelSolverTests
{
    [MenuItem("Tools/ColorLoop/Tests/Level Solver")]
    private static void Run()
    {
        try
        {
            TestSolutionAndReplay();
            TestInsufficientAmmo();
            TestSearchLimit();

            Debug.Log(
                "LevelSolver testleri geçti: çözüm arama, " +
                "çözümü tekrar oynatma, kaynak bağımsızlığı, " +
                "mühimmat kontrolü ve arama sınırı.");

            EditorUtility.DisplayDialog(
                "Test geçti",
                "Solver çözüm buldu ve çözüm tekrar oynatılarak doğrulandı.",
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

    private static void TestSolutionAndReplay()
    {
        // İlk shooter yeşil, tahta kırmızı.
        // Solver ikinci shooter'ı da göndermeyi başarmalı.
        GameSimulator source = CreateSimulator(
            new[] { 0 },
            new[]
            {
                new ShooterState(3, 2),
                new ShooterState(0, 1)
            });

        SolverResult result = LevelSolver.Solve(
            source,
            new SolverSettings
            {
                MaxVisitedStates = 1000,
                MaxDepth = 64,
                TimeLimitMilliseconds = 5000,
                WaitSeconds = 0.25d
            });

        Require(result.Status == SolverStatus.Solved,
            "Basit bölüm için çözüm bulunamadı.");

        Require(result.Actions.Length > 0,
            "Çözüm hamleleri boş.");

        Require(source.State.Board.RemainingBlocks == 1 &&
                source.State.NextQueueIndex == 0 &&
                source.State.Time == 0d,
            "Solver kaynak simülasyonu değiştirdi.");

        GameSimulator replay = source.Clone();

        foreach (SolverAction action in result.Actions)
        {
            if (action.Type == SolverActionType.Launch)
            {
                Require(
                    replay.TryLaunch(action.ShooterIndex) ==
                    LaunchResult.Success,
                    "Çözümde geçersiz gönderme hamlesi bulundu.");
            }
            else
            {
                replay.Advance(action.Seconds);
            }
        }

        Require(replay.State.Status == SimulationStatus.Won,
            "Bulunan çözüm tekrar oynatıldığında kazanmadı.");
    }

    private static void TestInsufficientAmmo()
    {
        GameSimulator source = CreateSimulator(
            new[] { 0, 0 },
            new[] { new ShooterState(0, 1) });

        SolverResult result = LevelSolver.Solve(source);

        Require(result.Status == SolverStatus.InsufficientAmmo,
            "Eksik mühimmat tespit edilmedi.");

        Require(result.VisitedStates == 0,
            "Mühimmat eksikken gereksiz çözüm araması yapıldı.");
    }

    private static void TestSearchLimit()
    {
        GameSimulator source = CreateSimulator(
            new[] { 0 },
            new[] { new ShooterState(0, 1) });

        SolverResult result = LevelSolver.Solve(
            source,
            new SolverSettings
            {
                MaxVisitedStates = 100,
                MaxDepth = 1,
                TimeLimitMilliseconds = 5000,
                WaitSeconds = 0.25d
            });

        Require(
            result.Status == SolverStatus.NotFoundWithinLimits,
            "Derinlik sınırı sonucu yanlış raporlandı.");

        Require(result.Actions.Length == 0,
            "Tamamlanmamış yol çözüm olarak döndürüldü.");
    }

    private static GameSimulator CreateSimulator(
        int[] cells,
        ShooterState[] shooters)
    {
        var board = new BoardState(cells.Length, 1, cells);

        var state = new SimulationState(
            board,
            shooters,
            2,
            2);

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