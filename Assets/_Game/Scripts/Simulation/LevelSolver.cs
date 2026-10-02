using System;
using System.Collections.Generic;
using System.Diagnostics;

public enum SolverStatus
{
    Solved,
    InsufficientAmmo,
    NotFoundWithinLimits
}

public enum SolverActionType
{
    Launch,
    Wait
}

public readonly struct SolverAction
{
    public SolverActionType Type { get; }
    public int ShooterIndex { get; }
    public double Seconds { get; }

    private SolverAction(
        SolverActionType type,
        int shooterIndex,
        double seconds)
    {
        Type = type;
        ShooterIndex = shooterIndex;
        Seconds = seconds;
    }

    public static SolverAction Launch(int shooterIndex)
    {
        return new SolverAction(
            SolverActionType.Launch,
            shooterIndex,
            0d);
    }

    public static SolverAction Wait(double seconds)
    {
        return new SolverAction(
            SolverActionType.Wait,
            -1,
            seconds);
    }
}

public sealed class SolverSettings
{
    public int MaxVisitedStates = 5000;
    public int MaxDepth = 256;
    public int TimeLimitMilliseconds = 1000;
    public double WaitSeconds = 0.25d;
}

public sealed class SolverResult
{
    public SolverStatus Status { get; }
    public SolverAction[] Actions { get; }
    public int VisitedStates { get; }
    public long ElapsedMilliseconds { get; }
    public string Message { get; }

    internal SolverResult(
        SolverStatus status,
        SolverAction[] actions,
        int visitedStates,
        long elapsedMilliseconds,
        string message)
    {
        Status = status;
        Actions = actions;
        VisitedStates = visitedStates;
        ElapsedMilliseconds = elapsedMilliseconds;
        Message = message;
    }
}

public static class LevelSolver
{
    public static SolverResult Solve(
        GameSimulator source,
        SolverSettings settings = null)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (settings == null)
            settings = new SolverSettings();

        ValidateSettings(settings);

        if (source.State.Status == SimulationStatus.Won)
        {
            return new SolverResult(
                SolverStatus.Solved,
                Array.Empty<SolverAction>(),
                0,
                0,
                "Tahta zaten tamamlanmış.");
        }

        if (TryFindAmmoDeficit(source.State, out string deficit))
        {
            return new SolverResult(
                SolverStatus.InsufficientAmmo,
                Array.Empty<SolverAction>(),
                0,
                0,
                deficit);
        }

        var search = new SearchContext(settings);
        return search.Run(source);
    }

    private static bool TryFindAmmoDeficit(
        SimulationState state,
        out string message)
    {
        var requiredByColor = new Dictionary<int, long>();

        for (int y = 0; y < state.Board.Height; y++)
        {
            for (int x = 0; x < state.Board.Width; x++)
            {
                int color = state.Board.GetColor(x, y);

                if (color == BoardState.EmptyCell)
                    continue;

                requiredByColor.TryGetValue(color, out long count);
                requiredByColor[color] = count + 1;
            }
        }

        for (int i = 0; i < state.ShooterCount; i++)
        {
            var shooter = state.GetShooter(i);

            if (shooter.Location == ShooterLocation.Finished)
                continue;

            int color = shooter.State.ColorId;

            if (!requiredByColor.TryGetValue(color, out long required))
                continue;

            requiredByColor[color] =
                required - shooter.State.RemainingAmmo;
        }

        foreach (var pair in requiredByColor)
        {
            if (pair.Value <= 0)
                continue;

            message =
                $"Renk {pair.Key} için {pair.Value} mermi eksik.\n" +
                "Mevcut shooter listesiyle bölüm tamamlanamaz.";

            return true;
        }

        message = null;
        return false;
    }

    private static void ValidateSettings(SolverSettings settings)
    {
        if (settings.MaxVisitedStates < 1)
            throw new ArgumentOutOfRangeException(
                nameof(settings.MaxVisitedStates));

        // Bu sürüm derinlik öncelikli, özyinelemeli arama kullanıyor.
        if (settings.MaxDepth < 1 || settings.MaxDepth > 512)
            throw new ArgumentOutOfRangeException(
                nameof(settings.MaxDepth),
                "Derinlik 1 ile 512 arasında olmalı.");

        if (settings.TimeLimitMilliseconds < 1)
            throw new ArgumentOutOfRangeException(
                nameof(settings.TimeLimitMilliseconds));

        if (double.IsNaN(settings.WaitSeconds) ||
            double.IsInfinity(settings.WaitSeconds) ||
            settings.WaitSeconds <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings.WaitSeconds));
        }
    }

    private sealed class SearchContext
    {
        private readonly int maxVisitedStates;
        private readonly int maxDepth;
        private readonly int timeLimitMilliseconds;
        private readonly double waitSeconds;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly List<SolverAction> currentPath;

        private SolverAction[] solution;
        private int visitedStates;
        private bool globalLimitReached;

        // Her derinlik kendi sonuç dizisini tekrar kullanır.
        private int[][] candidateBuffers;

        public SearchContext(SolverSettings settings)
        {
            maxVisitedStates = settings.MaxVisitedStates;
            maxDepth = settings.MaxDepth;
            timeLimitMilliseconds = settings.TimeLimitMilliseconds;
            waitSeconds = settings.WaitSeconds;

            currentPath = new List<SolverAction>(maxDepth);
        }

        public SolverResult Run(GameSimulator source)
        {
            candidateBuffers = new int[maxDepth][];

            stopwatch.Start();

            // Kaynak durumu koruyarak aramaya başla.
            bool solved = Visit(source.Clone(), 0);

            stopwatch.Stop();

            if (solved)
            {
                return new SolverResult(
                    SolverStatus.Solved,
                    solution,
                    visitedStates,
                    stopwatch.ElapsedMilliseconds,
                    "Kazandıran bir hamle dizisi bulundu.");
            }

            return new SolverResult(
                SolverStatus.NotFoundWithinLimits,
                Array.Empty<SolverAction>(),
                visitedStates,
                stopwatch.ElapsedMilliseconds,
                "Seçilen bekleme aralığı ve arama sınırlarıyla " +
                "çözüm bulunamadı. Bu sonuç çözümsüzlük kanıtı değildir.");
        }

        private bool Visit(GameSimulator simulator, int depth)
        {
            // Son izin verilen hamle kazanmış olabilir.
            if (simulator.State.Status == SimulationStatus.Won)
            {
                solution = currentPath.ToArray();
                return true;
            }

            if (simulator.State.Status == SimulationStatus.Lost)
                return false;

            if (ReachedGlobalLimit())
                return false;

            visitedStates++;

            if (depth >= maxDepth)
                return false;

            int[] candidates = candidateBuffers[depth];

            if (candidates == null)
            {
                candidates =
                    new int[
                        simulator.State.WaitingSlotCount +
                        simulator.State.QueueLaneCount];

                candidateBuffers[depth] = candidates;
            }

            int count = simulator.GetLaunchableShooters(candidates);

            // Önce geçerli gönderme hamlelerini dene.
            for (int i = 0; i < count; i++)
            {
                if (ReachedGlobalLimit())
                    return false;

                int shooterIndex = candidates[i];

                GameSimulator branch = simulator.Clone();

                if (branch.TryLaunch(shooterIndex) != LaunchResult.Success)
                    continue;

                currentPath.Add(SolverAction.Launch(shooterIndex));

                bool solved = Visit(branch, depth + 1);

                currentPath.RemoveAt(currentPath.Count - 1);

                if (solved)
                    return true;

                if (globalLimitReached)
                    return false;
            }

            if (ReachedGlobalLimit())
                return false;

            // Gönderme mümkün olsa bile beklemek ayrı bir seçenektir.
            bool canWait = SimulationWaitAction.TryCreateBranch(
                simulator,
                waitSeconds,
                out GameSimulator waitingBranch);

            if (!canWait)
                return false;

            currentPath.Add(SolverAction.Wait(waitSeconds));

            bool solvedAfterWaiting = Visit(waitingBranch, depth + 1);

            currentPath.RemoveAt(currentPath.Count - 1);

            return solvedAfterWaiting;
        }

        private bool ReachedGlobalLimit()
        {
            if (globalLimitReached)
                return true;

            globalLimitReached =
                visitedStates >= maxVisitedStates ||
                stopwatch.ElapsedMilliseconds >= timeLimitMilliseconds;

            return globalLimitReached;
        }
    }
}