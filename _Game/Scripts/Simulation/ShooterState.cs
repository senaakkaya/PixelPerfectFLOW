using System;

public sealed class ShooterState
{
    public int ColorId { get; }
    public int InitialAmmo { get; }
    public int RemainingAmmo { get; private set; }

    public bool HasAmmo => RemainingAmmo > 0;

    public ShooterState(int colorId, int initialAmmo)
    {
        if (colorId < 0)
            throw new ArgumentOutOfRangeException(nameof(colorId));

        if (initialAmmo <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialAmmo));

        ColorId = colorId;
        InitialAmmo = initialAmmo;
        RemainingAmmo = initialAmmo;
    }

    private ShooterState(ShooterState source)
    {
        ColorId = source.ColorId;
        InitialAmmo = source.InitialAmmo;
        RemainingAmmo = source.RemainingAmmo;
    }

    public bool TryConsumeAmmo()
    {
        if (!HasAmmo)
            return false;

        RemainingAmmo--;
        return true;
    }

    public ShooterState Clone()
    {
        return new ShooterState(this);
    }
}