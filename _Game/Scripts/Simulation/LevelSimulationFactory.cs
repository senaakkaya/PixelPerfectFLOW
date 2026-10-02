using System;

public static class LevelSimulationFactory
{
    public static GameSimulator Create(
        LevelData level,
        int conveyorCapacity,
        int waitingSlotCount,
        double cellSize,
        double distanceFromBoard,
        double entryClearance,
        double movementSpeed)
    {
        if (level == null)
            throw new ArgumentNullException(nameof(level));

        if (!level.HasBoardData)
        {
            throw new InvalidOperationException(
                "Bölümün tahta verisi eksik veya boyutları hatalı.");
        }

        if (level.Palette == null)
        {
            throw new InvalidOperationException(
                "Bölüme renk paleti atanmamış.");
        }

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
                        $"Geçersiz hücre rengi: ({x}, {y}), renk {color}.");
                }

                cells[y * level.Width + x] = color;
            }
        }

        var shooters = new ShooterState[level.ShooterCount];
        var lanes = new int[level.ShooterCount];

        for (int i = 0; i < shooters.Length; i++)
        {
            ShooterData data = level.GetShooter(i);

            if (!level.Palette.Contains(data.ColorId))
            {
                throw new InvalidOperationException(
                    $"Shooter {i}: geçersiz renk.");
            }

            if (data.Ammo <= 0)
            {
                throw new InvalidOperationException(
                    $"Shooter {i}: mühimmat sıfırdan büyük olmalı.");
            }

            if (data.LaneIndex < 0 ||
                data.LaneIndex >= LevelData.QueueLaneCount)
            {
                throw new InvalidOperationException(
                    $"Shooter {i}: Lane Index 0, 1 veya 2 olmalı.");
            }

            shooters[i] = new ShooterState(
                data.ColorId,
                data.Ammo);

            lanes[i] = data.LaneIndex;
        }

        var state = new SimulationState(
            new BoardState(level.Width, level.Height, cells),
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
}