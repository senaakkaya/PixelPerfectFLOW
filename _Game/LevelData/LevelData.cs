using System;
using UnityEngine;

[Serializable]
public struct ShooterData
{
    [SerializeField, Min(0)]
    private int colorId;

    [SerializeField, Min(1)]
    private int ammo;

    [Tooltip("0: Sol sıra, 1: Orta sıra, 2: Sağ sıra")]
    [SerializeField, Range(0, 2)]
    private int laneIndex;

    public int ColorId => colorId;
    public int Ammo => ammo;
    public int LaneIndex => laneIndex;

    public ShooterData(int colorId, int ammo, int laneIndex = 0)
    {
        this.colorId = colorId;
        this.ammo = ammo;
        this.laneIndex = laneIndex;
    }
}

[CreateAssetMenu(
    fileName = "Level_001",
    menuName = "ColorLoop/Level Data")]
public sealed class LevelData : ScriptableObject
{
    public const int EmptyCell = -1;
    public const int QueueLaneCount = 3;

    [Header("Renk Paleti")]
    [SerializeField]
    private ColorPalette palette;

    [Header("Tahta Boyutu")]
    [SerializeField, Min(1)]
    private int width = 32;

    [SerializeField, Min(1)]
    private int height = 32;

    [SerializeField, HideInInspector]
    private int[] cells = Array.Empty<int>();

    [Header("Bölüm Kuralları")]
    [SerializeField, Min(1)]
    private int conveyorCapacity = 2;

    [SerializeField, Min(1)]
    private int waitingCapacity = 5;

    [Header("Shooter Sırası")]
    [SerializeField]
    private ShooterData[] shooters = Array.Empty<ShooterData>();

    public ColorPalette Palette => palette;

    public int Width => width;
    public int Height => height;

    public int CellCount => cells != null ? cells.Length : 0;
    public int ShooterCount => shooters != null ? shooters.Length : 0;

    public int ConveyorCapacity => Mathf.Max(1, conveyorCapacity);
    public int WaitingCapacity => Mathf.Max(1, waitingCapacity);

    public bool HasBoardData =>
        width > 0 &&
        height > 0 &&
        cells != null &&
        (long)width * height == cells.Length;

    public int GetCell(int x, int y)
    {
        return cells[y * width + x];
    }

    public ShooterData GetShooter(int index)
    {
        return shooters[index];
    }
}