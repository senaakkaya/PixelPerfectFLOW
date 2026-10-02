using System;

public enum ShooterLocation
{
    Queue,
    Conveyor,
    Waiting,
    Finished
}

public enum SimulationStatus
{
    Running,
    Won,
    Lost
}

public sealed class SimulationState
{
    public sealed class ShooterEntry
    {
        public ShooterState State { get; }
        public int LaneIndex { get; }

        public ShooterLocation Location { get; internal set; }
        public double Distance { get; internal set; }
        public int NextShotPoint { get; internal set; }
        public long EntryOrder { get; internal set; }

        internal ShooterEntry(ShooterState source, int laneIndex)
        {
            State = source.Clone();
            LaneIndex = laneIndex;

            Location = ShooterLocation.Queue;
            Distance = 0d;
            NextShotPoint = 0;
            EntryOrder = -1;
        }

        private ShooterEntry(ShooterEntry source)
        {
            State = source.State.Clone();
            LaneIndex = source.LaneIndex;
            Location = source.Location;
            Distance = source.Distance;
            NextShotPoint = source.NextShotPoint;
            EntryOrder = source.EntryOrder;
        }

        internal ShooterEntry Clone()
        {
            return new ShooterEntry(this);
        }
    }

    private readonly ShooterEntry[] shooters;
    private readonly int[] waitingSlots;

    private readonly int[] laneHeads;
    private readonly int[] nextInLane;

    public BoardState Board { get; }

    public int ShooterCount => shooters.Length;
    public int WaitingSlotCount => waitingSlots.Length;
    public int QueueLaneCount => laneHeads.Length;

    public int ConveyorCapacity { get; }

    public double Time { get; internal set; }
    public long NextEntryOrder { get; internal set; }
    public SimulationStatus Status { get; internal set; }

    // Eski tek sıra testleri için korunuyor.
    // Yeni kodda GetQueueHead(lane) kullanılacak.
    public int NextQueueIndex =>
        laneHeads[0] >= 0 ? laneHeads[0] : ShooterCount;

    public int ActiveShooterCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < shooters.Length; i++)
            {
                if (shooters[i].Location == ShooterLocation.Conveyor)
                    count++;
            }

            return count;
        }
    }

    public SimulationState(
        BoardState sourceBoard,
        ShooterState[] sourceShooters,
        int conveyorCapacity,
        int waitingSlotCount,
        int[] laneIndices = null)
    {
        if (sourceBoard == null)
            throw new ArgumentNullException(nameof(sourceBoard));

        if (sourceShooters == null)
            throw new ArgumentNullException(nameof(sourceShooters));

        if (conveyorCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(conveyorCapacity));

        if (waitingSlotCount < 1)
            throw new ArgumentOutOfRangeException(nameof(waitingSlotCount));

        if (laneIndices != null &&
            laneIndices.Length != sourceShooters.Length)
        {
            throw new ArgumentException(
                "Sıra bilgisi ve shooter sayısı eşleşmeli.",
                nameof(laneIndices));
        }

        Board = sourceBoard.Clone();
        ConveyorCapacity = conveyorCapacity;

        shooters = new ShooterEntry[sourceShooters.Length];
        nextInLane = new int[sourceShooters.Length];

        laneHeads = new int[3];

        for (int lane = 0; lane < laneHeads.Length; lane++)
            laneHeads[lane] = -1;

        for (int i = 0; i < sourceShooters.Length; i++)
        {
            if (sourceShooters[i] == null)
            {
                throw new ArgumentException(
                    $"Shooter {i} boş.",
                    nameof(sourceShooters));
            }

            int lane = laneIndices != null ? laneIndices[i] : 0;

            if (lane < 0 || lane >= laneHeads.Length)
            {
                throw new ArgumentException(
                    $"Shooter {i}: sıra 0, 1 veya 2 olmalı.",
                    nameof(laneIndices));
            }

            shooters[i] = new ShooterEntry(sourceShooters[i], lane);
        }

        for (int i = shooters.Length - 1; i >= 0; i--)
        {
            int lane = shooters[i].LaneIndex;

            nextInLane[i] = laneHeads[lane];
            laneHeads[lane] = i;
        }

        waitingSlots = new int[waitingSlotCount];

        for (int i = 0; i < waitingSlots.Length; i++)
            waitingSlots[i] = -1;

        Time = 0d;
        NextEntryOrder = 0;

        Status = Board.RemainingBlocks == 0
            ? SimulationStatus.Won
            : SimulationStatus.Running;
    }

    private SimulationState(SimulationState source)
    {
        Board = source.Board.Clone();
        ConveyorCapacity = source.ConveyorCapacity;

        shooters = new ShooterEntry[source.shooters.Length];

        for (int i = 0; i < shooters.Length; i++)
            shooters[i] = source.shooters[i].Clone();

        waitingSlots = (int[])source.waitingSlots.Clone();
        laneHeads = (int[])source.laneHeads.Clone();
        nextInLane = (int[])source.nextInLane.Clone();

        Time = source.Time;
        NextEntryOrder = source.NextEntryOrder;
        Status = source.Status;
    }

    public SimulationState Clone()
    {
        return new SimulationState(this);
    }

    public ShooterEntry GetShooter(int index)
    {
        if (index < 0 || index >= shooters.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return shooters[index];
    }

    public int GetQueueHead(int lane)
    {
        if (lane < 0 || lane >= laneHeads.Length)
            throw new ArgumentOutOfRangeException(nameof(lane));

        return laneHeads[lane];
    }

    internal void RemoveQueueHead(int shooterIndex)
    {
        ShooterEntry shooter = GetShooter(shooterIndex);
        int lane = shooter.LaneIndex;

        if (shooter.Location != ShooterLocation.Queue ||
            laneHeads[lane] != shooterIndex)
        {
            throw new InvalidOperationException(
                "Yalnızca sıranın başındaki shooter çıkarılabilir.");
        }

        laneHeads[lane] = nextInLane[shooterIndex];
    }

    public int GetWaitingOccupant(int index)
    {
        if (index < 0 || index >= waitingSlots.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return waitingSlots[index];
    }

    internal void SetWaitingOccupant(int slot, int shooterIndex)
    {
        if (slot < 0 || slot >= waitingSlots.Length)
            throw new ArgumentOutOfRangeException(nameof(slot));

        if (shooterIndex < -1 || shooterIndex >= shooters.Length)
            throw new ArgumentOutOfRangeException(nameof(shooterIndex));

        waitingSlots[slot] = shooterIndex;
    }
}