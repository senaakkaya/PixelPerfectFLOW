using System;
using UnityEngine;

public sealed class BoardManager : MonoBehaviour
{
    [SerializeField] private BoardRenderer boardRenderer;

    private BoardState state;

    public int Width => state != null ? state.Width : 0;
    public int Height => state != null ? state.Height : 0;

    public int RemainingBlocks =>
        state != null ? state.RemainingBlocks : 0;

    public bool IsReady => state != null;

    public event Action<int> BlockRemoved;
    public event Action BoardCleared;

    private void Awake()
    {
        InitializeBoard();
    }

    private void InitializeBoard()
    {
        if (boardRenderer == null)
        {
            Debug.LogError(
                "BoardManager: Board Renderer alanını doldur.",
                this);

            enabled = false;
            return;
        }

        LevelData level = boardRenderer.Level;

        if (level == null ||
            !level.HasBoardData ||
            level.Palette == null ||
            level.Palette.Count == 0)
        {
            Debug.LogError(
                "BoardManager: Bölüm verisi veya paleti geçersiz.",
                this);

            enabled = false;
            return;
        }

        int[] sourceCells = new int[level.CellCount];

        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                int colorId = level.GetCell(x, y);

                if (colorId != BoardState.EmptyCell &&
                    !level.Palette.Contains(colorId))
                {
                    Debug.LogError(
                        $"Geçersiz renk: {colorId}, hücre: ({x}, {y})",
                        this);

                    enabled = false;
                    return;
                }

                sourceCells[y * level.Width + x] = colorId;
            }
        }

        state = new BoardState(
            level.Width, level.Height, sourceCells);
    }

    public bool IsInside(int x, int y)
    {
        return state != null && state.IsInside(x, y);
    }

    public int GetColor(int x, int y)
    {
        return state != null
            ? state.GetColor(x, y)
            : BoardState.EmptyCell;
    }

    public bool TryRemoveBlock(int x, int y, int shooterColorId)
    {
        if (state == null)
            return false;

        if (!state.TryRemoveBlock(
                x, y, shooterColorId, out int removedIndex))
        {
            return false;
        }

        // Unity tarafındaki görsel ve oyun sistemlerini bilgilendir.
        BlockRemoved?.Invoke(removedIndex);

        if (state.RemainingBlocks == 0)
            BoardCleared?.Invoke();

        return true;
    }

    public bool TryFindFirstBlock(
        int startX,
        int startY,
        int stepX,
        int stepY,
        out int hitX,
        out int hitY,
        out int colorId)
    {
        hitX = -1;
        hitY = -1;
        colorId = BoardState.EmptyCell;

        if (state == null)
            return false;

        return state.TryFindFirstBlock(
            startX,
            startY,
            stepX,
            stepY,
            out hitX,
            out hitY,
            out colorId);
    }

    public BoardState CreateSnapshot()
    {
        return state?.Clone();
    }

    [ContextMenu("Test/İlk Dolu Bloğu Kaldır")]
    private void TestRemoveFirstBlock()
    {
        if (!Application.isPlaying || !IsReady)
            return;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int colorId = GetColor(x, y);

                if (colorId == BoardState.EmptyCell)
                    continue;

                bool removed = TryRemoveBlock(x, y, colorId);

                Debug.Log(
                    $"Silindi: {removed} | Hücre: ({x}, {y}) | " +
                    $"Kalan: {RemainingBlocks}",
                    this);

                return;
            }
        }

        Debug.Log("Tahtada silinecek blok kalmadı.", this);
    }

    [ContextMenu("Test/Kopyanın Bağımsızlığını Kontrol Et")]
    private void TestSnapshot()
    {
        if (!Application.isPlaying || !IsReady)
            return;

        BoardState copy = CreateSnapshot();
        int originalCount = RemainingBlocks;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int colorId = copy.GetColor(x, y);

                if (colorId == BoardState.EmptyCell)
                    continue;

                bool removed = copy.TryRemoveBlock(
                    x, y, colorId, out _);

                bool passed =
                    removed &&
                    copy.GetColor(x, y) == BoardState.EmptyCell &&
                    copy.RemainingBlocks == originalCount - 1 &&
                    GetColor(x, y) == colorId &&
                    RemainingBlocks == originalCount;

                if (passed)
                {
                    Debug.Log(
                        "Kopya testi geçti: yalnızca kopyadaki blok " +
                        "silindi, asıl tahta değişmedi.",
                        this);
                }
                else
                {
                    Debug.LogError("Kopya testi başarısız.", this);
                }

                return;
            }
        }

        Debug.Log(
            "Kopya testi için tahtada en az bir blok bulunmalı.",
            this);
    }
}