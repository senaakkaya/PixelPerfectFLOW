using System;

public enum LaunchResult
{
    Success,
    GameEnded,
    InvalidShooter,
    NotQueueFront,
    AlreadyOnConveyor,
    ShooterFinished,
    NoAmmo,
    ConveyorFull,
    EntryBlocked,
    InvalidState
}

public sealed class GameSimulator
{
    public SimulationState State { get; }

    public double PathLength => 2d * (pathWidth + pathDepth);
    public double MovementSpeed { get; }

    private readonly double cellSize;
    private readonly double margin;
    private readonly double pathWidth;
    private readonly double pathDepth;
    private readonly double entryClearanceSquared;

    private int ShotPointCount =>
        2 * (State.Board.Width + State.Board.Height);

    public GameSimulator(
        SimulationState state,
        double cellSize,
        double distanceFromBoard,
        double entryClearance,
        double movementSpeed = 1.5d)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        ValidatePositive(cellSize, nameof(cellSize));
        ValidatePositive(distanceFromBoard, nameof(distanceFromBoard));
        ValidatePositive(entryClearance, nameof(entryClearance));
        ValidatePositive(movementSpeed, nameof(movementSpeed));

        State = state;

        this.cellSize = cellSize;
        margin = distanceFromBoard;

        pathWidth = state.Board.Width * cellSize + 2d * margin;
        pathDepth = state.Board.Height * cellSize + 2d * margin;

        entryClearanceSquared = entryClearance * entryClearance;
        MovementSpeed = movementSpeed;
    }

    // Tahta, shooter'lar ve simülasyon ayarlarıyla bağımsız kopya üretir.
    public GameSimulator Clone()
    {
        return new GameSimulator(
            State.Clone(),
            cellSize,
            margin,
            Math.Sqrt(entryClearanceSquared),
            MovementSpeed);
    }

    // Oyun durumunu değiştirmeden gönderme koşullarını kontrol eder.
    public LaunchResult CanLaunch(int shooterIndex)
    {
        if (State.Status != SimulationStatus.Running)
            return LaunchResult.GameEnded;

        if (shooterIndex < 0 || shooterIndex >= State.ShooterCount)
            return LaunchResult.InvalidShooter;

        var shooter = State.GetShooter(shooterIndex);

        if (shooter.Location == ShooterLocation.Conveyor)
            return LaunchResult.AlreadyOnConveyor;

        if (shooter.Location == ShooterLocation.Finished)
            return LaunchResult.ShooterFinished;

        if (!shooter.State.HasAmmo)
            return LaunchResult.NoAmmo;

        if (shooter.Location == ShooterLocation.Queue)
        {
            int head = State.GetQueueHead(shooter.LaneIndex);

            if (head != shooterIndex)
                return LaunchResult.NotQueueFront;
        }
        else if (shooter.Location == ShooterLocation.Waiting)
        {
            if (FindWaitingSlot(shooterIndex) < 0)
                return LaunchResult.InvalidState;
        }
        else
        {
            return LaunchResult.InvalidState;
        }

        if (State.ActiveShooterCount >= State.ConveyorCapacity)
            return LaunchResult.ConveyorFull;

        if (!IsEntryClear())
            return LaunchResult.EntryBlocked;

        if (State.NextEntryOrder == long.MaxValue)
            return LaunchResult.InvalidState;

        return LaunchResult.Success;
    }

    // Geçerli bir gönderme hamlesini uygular.
    public LaunchResult TryLaunch(int shooterIndex)
    {
        LaunchResult result = CanLaunch(shooterIndex);

        if (result != LaunchResult.Success)
            return result;

        var shooter = State.GetShooter(shooterIndex);

        if (shooter.Location == ShooterLocation.Waiting)
        {
            int slot = FindWaitingSlot(shooterIndex);
            State.SetWaitingOccupant(slot, -1);
        }
        else
        {
            State.RemoveQueueHead(shooterIndex);
        }

        shooter.Location = ShooterLocation.Conveyor;
        shooter.Distance = 0d;
        shooter.NextShotPoint = 0;
        shooter.EntryOrder = State.NextEntryOrder;

        State.NextEntryOrder++;

        return LaunchResult.Success;
    }
    // Sonuç dizisinin yalnızca dönen sayı kadar elemanı geçerlidir.
    public int GetLaunchableShooters(int[] results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        if (State.Status != SimulationStatus.Running)
            return 0;

        // Tek sıralı eski testlerin küçük dizileriyle de uyumlu.
        int candidateCount = 0;

        for (int lane = 0; lane < State.QueueLaneCount; lane++)
        {
            if (State.GetQueueHead(lane) >= 0)
                candidateCount++;
        }

        for (int slot = 0; slot < State.WaitingSlotCount; slot++)
        {
            if (State.GetWaitingOccupant(slot) >= 0)
                candidateCount++;
        }

        if (results.Length < candidateCount)
        {
            throw new ArgumentException(
                $"Sonuç dizisi en az {candidateCount} eleman içermeli.",
                nameof(results));
        }

        int count = 0;

        for (int lane = 0; lane < State.QueueLaneCount; lane++)
        {
            int shooterIndex = State.GetQueueHead(lane);

            if (shooterIndex < 0)
                continue;

            if (CanLaunch(shooterIndex) == LaunchResult.Success)
                results[count++] = shooterIndex;
        }

        for (int slot = 0; slot < State.WaitingSlotCount; slot++)
        {
            int shooterIndex = State.GetWaitingOccupant(slot);

            if (shooterIndex < 0)
                continue;

            if (CanLaunch(shooterIndex) == LaunchResult.Success)
                results[count++] = shooterIndex;
        }

        return count;
    }

    // Gerçek zamanda beklemeden simülasyonu ileri hesaplar.
    public void Advance(double seconds)
    {
        if (double.IsNaN(seconds) ||
            double.IsInfinity(seconds) ||
            seconds < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        if (State.Status != SimulationStatus.Running)
            return;

        double targetTime = State.Time + seconds;

        if (double.IsInfinity(targetTime))
            throw new ArgumentOutOfRangeException(nameof(seconds));

        while (State.Status == SimulationStatus.Running)
        {
            int nextShooter = FindNextEvent(
                out double eventDistance,
                out double travelDistance);

            double remainingTime = targetTime - State.Time;

            // Bant boşsa yalnızca simülasyon saatini ilerlet.
            if (nextShooter < 0)
            {
                State.Time = targetTime;
                return;
            }

            double timeUntilEvent = travelDistance / MovementSpeed;

            // Sonraki olay istenen zaman aralığının dışında.
            if (timeUntilEvent > remainingTime)
            {
                MoveActiveShooters(remainingTime * MovementSpeed);
                State.Time = targetTime;
                return;
            }

            // Tüm aktif shooter'ları sıradaki olayın zamanına getir.
            MoveActiveShooters(travelDistance);

            State.Time = Math.Min(
                targetTime,
                State.Time + timeUntilEvent);

            var shooter = State.GetShooter(nextShooter);

            // Olayı işleyen shooter'ı tam olay mesafesine yerleştir.
            shooter.Distance = eventDistance;

            ProcessEvent(nextShooter);

            // Aynı zamandaki diğer olaylar sonraki döngüde işlenir.
        }
    }

    private int FindNextEvent(
        out double eventDistance,
        out double travelDistance)
    {
        int selected = -1;

        eventDistance = 0d;
        travelDistance = double.PositiveInfinity;

        long selectedOrder = long.MaxValue;

        for (int i = 0; i < State.ShooterCount; i++)
        {
            var shooter = State.GetShooter(i);

            if (shooter.Location != ShooterLocation.Conveyor)
                continue;

            double nextDistance = GetNextEventDistance(shooter);

            double distanceToEvent = Math.Max(
                0d,
                nextDistance - shooter.Distance);

            bool earlier = distanceToEvent < travelDistance;

            bool sameTimeEarlierEntry =
                distanceToEvent == travelDistance &&
                shooter.EntryOrder < selectedOrder;

            if (!earlier && !sameTimeEarlierEntry)
                continue;

            selected = i;
            selectedOrder = shooter.EntryOrder;

            eventDistance = nextDistance;
            travelDistance = distanceToEvent;
        }

        return selected;
    }

    private double GetNextEventDistance(
        SimulationState.ShooterEntry shooter)
    {
        if (shooter.NextShotPoint >= ShotPointCount)
            return PathLength;

        GetShotPoint(
            shooter.NextShotPoint,
            out double distance,
            out _,
            out _,
            out _,
            out _);

        return distance;
    }

    private void MoveActiveShooters(double distance)
    {
        for (int i = 0; i < State.ShooterCount; i++)
        {
            var shooter = State.GetShooter(i);

            if (shooter.Location != ShooterLocation.Conveyor)
                continue;

            shooter.Distance = Math.Min(
                PathLength,
                shooter.Distance + distance);
        }
    }

    private void ProcessEvent(int shooterIndex)
    {
        var shooter = State.GetShooter(shooterIndex);

        if (shooter.NextShotPoint >= ShotPointCount)
        {
            CompleteLap(shooterIndex);
            return;
        }

        GetShotPoint(
            shooter.NextShotPoint,
            out _,
            out int startX,
            out int startY,
            out int stepX,
            out int stepY);

        // İsabet olmasa bile bu atış noktası geçildi.
        shooter.NextShotPoint++;

        if (!shooter.State.HasAmmo)
        {
            shooter.Location = ShooterLocation.Finished;
            return;
        }

        bool found = State.Board.TryFindFirstBlock(
            startX,
            startY,
            stepX,
            stepY,
            out int hitX,
            out int hitY,
            out int hitColor);

        if (!found || hitColor != shooter.State.ColorId)
            return;

        bool removed = State.Board.TryRemoveBlock(
            hitX,
            hitY,
            shooter.State.ColorId,
            out _);

        if (!removed)
            return;

        shooter.State.TryConsumeAmmo();

        if (!shooter.State.HasAmmo)
            shooter.Location = ShooterLocation.Finished;

        if (State.Board.RemainingBlocks == 0)
            State.Status = SimulationStatus.Won;
    }

    private void CompleteLap(int shooterIndex)
    {
        var shooter = State.GetShooter(shooterIndex);

        if (!shooter.State.HasAmmo)
        {
            shooter.Location = ShooterLocation.Finished;
            return;
        }

        for (int slot = 0; slot < State.WaitingSlotCount; slot++)
        {
            if (State.GetWaitingOccupant(slot) != -1)
                continue;

            State.SetWaitingOccupant(slot, shooterIndex);
            shooter.Location = ShooterLocation.Waiting;

            return;
        }

        // Dönen shooter'ı yerleştirecek boş slot yok.
        State.Status = SimulationStatus.Lost;
    }

    
    private void GetShotPoint(
        int index,
        out double distance,
        out int startX,
        out int startY,
        out int stepX,
        out int stepY)
    {
        BoardShotSchedule.GetPoint(
            State.Board.Width,
            State.Board.Height,
            cellSize,
            margin,
            index,
            out distance,
            out startX,
            out startY,
            out stepX,
            out stepY);
    }

    private int FindWaitingSlot(int shooterIndex)
    {
        for (int i = 0; i < State.WaitingSlotCount; i++)
        {
            if (State.GetWaitingOccupant(i) == shooterIndex)
                return i;
        }

        return -1;
    }

    private bool IsEntryClear()
    {
        for (int i = 0; i < State.ShooterCount; i++)
        {
            var shooter = State.GetShooter(i);

            if (shooter.Location != ShooterLocation.Conveyor)
                continue;

            double distance = shooter.Distance;

            if (double.IsNaN(distance) ||
                double.IsInfinity(distance) ||
                distance < 0d ||
                distance > PathLength)
            {
                return false;
            }

            GetOffsetFromEntry(
                distance,
                out double x,
                out double z);

            if (x * x + z * z < entryClearanceSquared)
                return false;
        }

        return true;
    }

    private void GetOffsetFromEntry(
        double distance,
        out double x,
        out double z)
    {
        if (distance < pathWidth)
        {
            x = distance;
            z = 0d;
            return;
        }

        if (distance < pathWidth + pathDepth)
        {
            x = pathWidth;
            z = distance - pathWidth;
            return;
        }

        if (distance < 2d * pathWidth + pathDepth)
        {
            x = 2d * pathWidth + pathDepth - distance;
            z = pathDepth;
            return;
        }

        x = 0d;
        z = PathLength - distance;
    }

    private static void ValidatePositive(double value, string name)
    {
        if (double.IsNaN(value) ||
            double.IsInfinity(value) ||
            value <= 0d)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}