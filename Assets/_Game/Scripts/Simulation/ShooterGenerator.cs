using System;
using System.Collections.Generic;
using System.Diagnostics;

public static class ShooterGenerator
{
    public static ShooterData[] Generate(
        LevelData level,
        int conveyorCapacity,
        int waitingSlotCount,
        double cellSize,
        double distanceFromBoard,
        double entryClearance,
        double movementSpeed,
        int maxShooters = 512,
        int timeLimitMilliseconds = 3000,
        int maxAmmoPerShooter = 30,
        int distributionSeed = 12345)
    {
        if (level == null)
            throw new ArgumentNullException(nameof(level));

        if (!level.HasBoardData || level.Palette == null)
        {
            throw new InvalidOperationException(
                "Bölümün tahta verisi veya renk paleti eksik.");
        }

        if (maxShooters < 1)
            throw new ArgumentOutOfRangeException(nameof(maxShooters));

        if (maxAmmoPerShooter < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAmmoPerShooter));
        }

        if (timeLimitMilliseconds < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeLimitMilliseconds));
        }

        var stopwatch = Stopwatch.StartNew();
        int[] cells = new int[level.CellCount];

        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                int color = level.GetCell(x, y);

                if (color != BoardState.EmptyCell &&
                    !level.Palette.Contains(color))
                {
                    throw new InvalidOperationException(
                        $"Geçersiz renk: ({x}, {y}), renk {color}.");
                }

                cells[y * level.Width + x] = color;
            }
        }

        var original = new BoardState(
            level.Width,
            level.Height,
            cells);

        if (original.RemainingBlocks == 0)
        {
            throw new InvalidOperationException(
                "Tahta boş. Önce çizim yap veya PNG aktar.");
        }

        var context = new GenerationContext(
            conveyorCapacity,
            waitingSlotCount,
            cellSize,
            distanceFromBoard,
            entryClearance,
            movementSpeed,
            stopwatch,
            timeLimitMilliseconds);

        BoardState working = original.Clone();

        int[] colorCounts = new int[level.Palette.Count];
        var generated = new List<ShooterData>();

        var random = new Random(distributionSeed);

        // Her üç shooter birer kez üç ayrı sıraya dağıtılır.
        int[] laneBag = new int[LevelData.QueueLaneCount];
        int previousLane = -1;

        while (working.RemainingBlocks > 0)
        {
            context.CheckTime();

            if (generated.Count >= maxShooters)
            {
                throw new InvalidOperationException(
                    $"Üretim {maxShooters} shooter sınırına ulaştı. " +
                    "Bölüm değiştirilmedi.");
            }

            CountColors(working, colorCounts);

            int bestColor = -1;
            int bestRemoved = 0;
            BoardState bestBoard = null;

            for (int color = 0; color < colorCounts.Length; color++)
            {
                context.CheckTime();

                if (colorCounts[color] == 0)
                    continue;

                int candidateAmmo = Math.Min(
                    colorCounts[color],
                    maxAmmoPerShooter);

                var candidateShooters = new[]
                {
                    new ShooterState(color, candidateAmmo)
                };

                GameSimulator candidate = context.CreateSimulator(
                    working,
                    candidateShooters);

                if (candidate.TryLaunch(0) != LaunchResult.Success)
                {
                    throw new InvalidOperationException(
                        "Aday shooter gönderilemedi.");
                }

                candidate.Advance(context.GetLapDuration(candidate));

                int removed =
                    working.RemainingBlocks -
                    candidate.State.Board.RemainingBlocks;

                if (removed <= bestRemoved)
                    continue;

                bestColor = color;
                bestRemoved = removed;
                bestBoard = candidate.State.Board;
            }

            if (bestColor < 0 || bestBoard == null)
            {
                throw new InvalidOperationException(
                    "Kalan tahtada ilerleme sağlayan shooter bulunamadı. " +
                    "Bölüm değiştirilmedi.");
            }

            int bagIndex = generated.Count % laneBag.Length;

            if (bagIndex == 0)
                ShuffleLanes(laneBag, random, previousLane);

            int laneIndex = laneBag[bagIndex];
            previousLane = laneIndex;

            generated.Add(new ShooterData(
                bestColor,
                bestRemoved,
                laneIndex));

            working = bestBoard;
        }

        ShooterData[] result = generated.ToArray();

        context.Verify(original, result, maxAmmoPerShooter);
        context.CheckTime();

        return result;
    }

    private static void ShuffleLanes(
        int[] lanes,
        Random random,
        int previousLane)
    {
        for (int i = 0; i < lanes.Length; i++)
            lanes[i] = i;

        for (int i = lanes.Length - 1; i > 0; i--)
        {
            int selected = random.Next(i + 1);
            Swap(lanes, i, selected);
        }

        // Önceki grubun sonuyla yeni grubun başı aynı olmasın.
        if (lanes.Length > 1 && lanes[0] == previousLane)
        {
            int selected = random.Next(1, lanes.Length);
            Swap(lanes, 0, selected);
        }
    }

    private static void Swap(int[] array, int a, int b)
    {
        int temporary = array[a];
        array[a] = array[b];
        array[b] = temporary;
    }

    private static void CountColors(BoardState board, int[] counts)
    {
        Array.Clear(counts, 0, counts.Length);

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                int color = board.GetColor(x, y);

                if (color != BoardState.EmptyCell)
                    counts[color]++;
            }
        }
    }

    private sealed class GenerationContext
    {
        private readonly int conveyorCapacity;
        private readonly int waitingSlotCount;

        private readonly double cellSize;
        private readonly double distanceFromBoard;
        private readonly double entryClearance;
        private readonly double movementSpeed;

        private readonly Stopwatch stopwatch;
        private readonly int timeLimitMilliseconds;

        public GenerationContext(
            int conveyorCapacity,
            int waitingSlotCount,
            double cellSize,
            double distanceFromBoard,
            double entryClearance,
            double movementSpeed,
            Stopwatch stopwatch,
            int timeLimitMilliseconds)
        {
            this.conveyorCapacity = conveyorCapacity;
            this.waitingSlotCount = waitingSlotCount;
            this.cellSize = cellSize;
            this.distanceFromBoard = distanceFromBoard;
            this.entryClearance = entryClearance;
            this.movementSpeed = movementSpeed;
            this.stopwatch = stopwatch;
            this.timeLimitMilliseconds = timeLimitMilliseconds;
        }

        public GameSimulator CreateSimulator(
            BoardState board,
            ShooterState[] shooters,
            int[] lanes = null)
        {
            var state = new SimulationState(
                board,
                shooters,
                conveyorCapacity,
                waitingSlotCount,
                lanes);

            return new GameSimulator(
                state,
                cellSize,
                distanceFromBoard,
                entryClearance,
                movementSpeed);
        }

        public double GetLapDuration(GameSimulator simulator)
        {
            return simulator.PathLength / simulator.MovementSpeed
                   + 0.000001d;
        }

        public void Verify(
            BoardState original,
            ShooterData[] data,
            int maxAmmoPerShooter)
        {
            var shooters = new ShooterState[data.Length];
            var lanes = new int[data.Length];

            long totalAmmo = 0;

            for (int i = 0; i < data.Length; i++)
            {
                CheckTime();

                if (data[i].Ammo <= 0 ||
                    data[i].Ammo > maxAmmoPerShooter)
                {
                    throw new InvalidOperationException(
                        $"Shooter {i}: mühimmat sınırı geçersiz.");
                }

                if (data[i].LaneIndex < 0 ||
                    data[i].LaneIndex >= LevelData.QueueLaneCount)
                {
                    throw new InvalidOperationException(
                        $"Shooter {i}: sıra bilgisi geçersiz.");
                }

                totalAmmo += data[i].Ammo;

                shooters[i] = new ShooterState(
                    data[i].ColorId,
                    data[i].Ammo);

                lanes[i] = data[i].LaneIndex;
            }

            if (totalAmmo != original.RemainingBlocks)
            {
                throw new InvalidOperationException(
                    "Toplam mühimmat küp sayısıyla eşleşmedi.");
            }

            GameSimulator replay = CreateSimulator(
                original,
                shooters,
                lanes);

            double lapDuration = GetLapDuration(replay);

            for (int i = 0; i < data.Length; i++)
            {
                CheckTime();

                LaunchResult launch = replay.TryLaunch(i);

                if (launch != LaunchResult.Success)
                {
                    throw new InvalidOperationException(
                        $"Üç sıra doğrulaması başarısız: " +
                        $"shooter {i}, sonuç {launch}.");
                }

                replay.Advance(lapDuration);

                if (replay.State.GetShooter(i).State.RemainingAmmo != 0)
                {
                    throw new InvalidOperationException(
                        $"Shooter {i} mühimmatını tamamlayamadı.");
                }

                if (replay.State.Status == SimulationStatus.Won)
                {
                    if (i != data.Length - 1)
                    {
                        throw new InvalidOperationException(
                            "Bölüm tamamlandıktan sonra " +
                            "kullanılmayan shooter kaldı.");
                    }

                    return;
                }

                if (replay.State.Status != SimulationStatus.Running)
                {
                    throw new InvalidOperationException(
                        "Üretilen sıra doğrulamada kaybetti.");
                }
            }

            throw new InvalidOperationException(
                "Üretilen liste tahtayı tamamen temizleyemedi.");
        }

        public void CheckTime()
        {
            if (stopwatch.ElapsedMilliseconds >= timeLimitMilliseconds)
            {
                throw new InvalidOperationException(
                    "Üretim süre sınırına ulaştı. " +
                    "Doğrulanmamış liste kaydedilmedi.");
            }
        }
    }
}