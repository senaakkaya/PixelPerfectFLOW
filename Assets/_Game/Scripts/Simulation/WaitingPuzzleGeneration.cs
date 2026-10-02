using System;
using System.Diagnostics;

public sealed class WaitingPuzzleResult
{
    public ShooterData[] Shooters { get; }
    public SolverAction[] Solution { get; }
    public int AttemptCount { get; }

    public WaitingPuzzleResult(
        ShooterData[] shooters,
        SolverAction[] solution,
        int attemptCount)
    {
        Shooters = shooters;
        Solution = solution;
        AttemptCount = attemptCount;
    }
}

public static class WaitingPuzzleGenerator
{
    public static WaitingPuzzleResult Generate(
        LevelData level,
        int maxCombinedAmmo = 60,
        int maxCandidates = 8,
        int totalTimeMilliseconds = 8000)
    {
        if (level == null)
            throw new ArgumentNullException(nameof(level));

        if (maxCombinedAmmo < 1)
            throw new ArgumentOutOfRangeException(nameof(maxCombinedAmmo));

        if (maxCandidates < 1)
            throw new ArgumentOutOfRangeException(nameof(maxCandidates));

        if (totalTimeMilliseconds < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalTimeMilliseconds));
        }

        var stopwatch = Stopwatch.StartNew();
        GameTuning tuning = GameTuning.Load();

        // Mevcut bölümün verilerini doğrula ve bağımsız tahta oluştur.
        GameSimulator source = LevelSimulationFactory.Create(
            level,
            level.ConveyorCapacity,
            level.WaitingCapacity,
            tuning.CellSize,
            tuning.DistanceFromBoard,
            tuning.EntryClearance,
            tuning.MovementSpeed);

        if (source.State.Board.RemainingBlocks == 0)
            throw new InvalidOperationException("Tahta boş.");

        var original = new ShooterData[level.ShooterCount];

        for (int i = 0; i < original.Length; i++)
            original[i] = level.GetShooter(i);

        int attempts = 0;

        // Önce listede birbirinden uzak, aynı renkteki çiftleri dene.
        for (int first = 0; first < original.Length; first++)
        {
            for (int second = original.Length - 1;
                 second > first;
                 second--)
            {
                if (stopwatch.ElapsedMilliseconds >= totalTimeMilliseconds ||
                    attempts >= maxCandidates)
                {
                    return null;
                }

                if (original[first].ColorId != original[second].ColorId)
                    continue;

                long combinedAmmo =
                    (long)original[first].Ammo + original[second].Ammo;

                if (combinedAmmo > maxCombinedAmmo)
                    continue;

                ShooterData[] candidate = MergePair(
                    original,
                    first,
                    second,
                    (int)combinedAmmo);

                GameSimulator simulation = CreateSimulation(
                    source.State.Board,
                    candidate,
                    level,
                    tuning);

                int remainingMilliseconds =
                    totalTimeMilliseconds -
                    (int)stopwatch.ElapsedMilliseconds;

                if (remainingMilliseconds <= 0)
                    return null;

                attempts++;

                var settings = new SolverSettings
                {
                    MaxVisitedStates = 5000,
                    MaxDepth = 512,

                    // Bir aday bütün üretim bütçesini tüketmesin.
                    TimeLimitMilliseconds = Math.Min(
                        1000,
                        remainingMilliseconds),

                    WaitSeconds = 1d
                };

                SolverResult result = LevelSolver.Solve(
                    simulation,
                    settings);

                if (result.Status != SolverStatus.Solved)
                    continue;

                // Solver'ın bulduğu yolu tekrar oynat.
                // Beklemeden yeniden gönderme gerçekten olmuş mu?
                if (!VerifyWaitingReturn(simulation, result.Actions))
                    continue;

                if (stopwatch.ElapsedMilliseconds >= totalTimeMilliseconds)
                    return null;

                return new WaitingPuzzleResult(
                    candidate,
                    result.Actions,
                    attempts);
            }
        }

        return null;
    }

    private static ShooterData[] MergePair(
        ShooterData[] source,
        int first,
        int second,
        int combinedAmmo)
    {
        var result = new ShooterData[source.Length - 1];
        int writeIndex = 0;

        for (int i = 0; i < source.Length; i++)
        {
            if (i == second)
                continue;

            ShooterData data = source[i];

            if (i == first)
            {
                data = new ShooterData(
                    data.ColorId,
                    combinedAmmo,
                    data.LaneIndex);
            }

            result[writeIndex++] = data;
        }

        return result;
    }

    private static GameSimulator CreateSimulation(
        BoardState board,
        ShooterData[] data,
        LevelData level,
        GameTuning tuning)
    {
        var shooters = new ShooterState[data.Length];
        var lanes = new int[data.Length];

        for (int i = 0; i < data.Length; i++)
        {
            shooters[i] = new ShooterState(
                data[i].ColorId,
                data[i].Ammo);

            lanes[i] = data[i].LaneIndex;
        }

        var state = new SimulationState(
            board,
            shooters,
            level.ConveyorCapacity,
            level.WaitingCapacity,
            lanes);

        return new GameSimulator(
            state,
            tuning.CellSize,
            tuning.DistanceFromBoard,
            tuning.EntryClearance,
            tuning.MovementSpeed);
    }

    private static bool VerifyWaitingReturn(
        GameSimulator source,
        SolverAction[] actions)
    {
        GameSimulator replay = source.Clone();
        bool usedWaitingReturn = false;

        for (int i = 0; i < actions.Length; i++)
        {
            SolverAction action = actions[i];

            if (action.Type == SolverActionType.Wait)
            {
                replay.Advance(action.Seconds);
                continue;
            }

            if (action.Type != SolverActionType.Launch)
                return false;

            var shooter = replay.State.GetShooter(action.ShooterIndex);

            bool wasWaiting =
                shooter.Location == ShooterLocation.Waiting;

            if (replay.TryLaunch(action.ShooterIndex) != LaunchResult.Success)
                return false;

            if (wasWaiting)
                usedWaitingReturn = true;
        }

        return usedWaitingReturn &&
               replay.State.Status == SimulationStatus.Won;
    }
}