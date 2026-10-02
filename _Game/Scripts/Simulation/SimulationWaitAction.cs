using System;

public static class SimulationWaitAction
{
    // Kaynak simülasyonu değiştirmeden bekleme hamlesi oluşturur.
    public static bool TryCreateBranch(
        GameSimulator source,
        double seconds,
        out GameSimulator branch)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (double.IsNaN(seconds) ||
            double.IsInfinity(seconds) ||
            seconds <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        branch = null;

        if (source.State.Status != SimulationStatus.Running)
            return false;

        // Bant boşsa beklemek hiçbir oyun durumunu değiştirmez.
        // Bu dalı üretmeyerek gereksiz aramayı önlüyoruz.
        if (source.State.ActiveShooterCount == 0)
            return false;

        double targetTime = source.State.Time + seconds;

        if (double.IsInfinity(targetTime))
            throw new ArgumentOutOfRangeException(nameof(seconds));

        // Çok küçük bir değer sayısal olarak zamanı ilerletemeyebilir.
        if (targetTime <= source.State.Time)
            return false;

        branch = source.Clone();
        branch.Advance(seconds);

        return true;
    }
}