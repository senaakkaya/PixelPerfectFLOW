using System;

public sealed class BoardState
{
    public const int EmptyCell = -1;

    private readonly int[] cells;

    public int Width { get; }
    public int Height { get; }
    public int RemainingBlocks { get; private set; }

    public BoardState(int width, int height, int[] sourceCells)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        if (sourceCells == null)
            throw new ArgumentNullException(nameof(sourceCells));

        if ((long)width * height != sourceCells.Length)
        {
            throw new ArgumentException(
                "Hücre sayısı, genişlik × yükseklik ile eşleşmiyor.",
                nameof(sourceCells));
        }

        Width = width;
        Height = height;

        // Dışarıdaki diziyi sahiplenme; bağımsız bir kopyasını tut.
        cells = new int[sourceCells.Length];

        for (int i = 0; i < sourceCells.Length; i++)
        {
            int colorId = sourceCells[i];

            if (colorId < EmptyCell)
            {
                throw new ArgumentException(
                    "Hücre değeri -1 veya geçerli bir renk kimliği olmalı.",
                    nameof(sourceCells));
            }

            cells[i] = colorId;

            if (colorId != EmptyCell)
                RemainingBlocks++;
        }
    }

    private BoardState(BoardState source)
    {
        Width = source.Width;
        Height = source.Height;
        RemainingBlocks = source.RemainingBlocks;

        cells = (int[])source.cells.Clone();
    }

    public BoardState Clone()
    {
        return new BoardState(this);
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < Width &&
               y >= 0 && y < Height;
    }

    public int GetColor(int x, int y)
    {
        if (!IsInside(x, y))
            return EmptyCell;

        return cells[y * Width + x];
    }

    public bool TryRemoveBlock(
        int x,
        int y,
        int shooterColorId,
        out int removedIndex)
    {
        removedIndex = -1;

        if (!IsInside(x, y) || shooterColorId < 0)
            return false;

        int index = y * Width + x;

        if (cells[index] != shooterColorId)
            return false;

        cells[index] = EmptyCell;
        RemainingBlocks--;

        removedIndex = index;
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
        colorId = EmptyCell;

        bool horizontal =
            (stepX == 1 || stepX == -1) && stepY == 0;

        bool vertical =
            (stepY == 1 || stepY == -1) && stepX == 0;

        if (!horizontal && !vertical)
            return false;

        int x = startX;
        int y = startY;

        // Başlangıç hücresi tahta içinde olmalıdır.
        while (IsInside(x, y))
        {
            int currentColor = cells[y * Width + x];

            if (currentColor != EmptyCell)
            {
                hitX = x;
                hitY = y;
                colorId = currentColor;
                return true;
            }

            x += stepX;
            y += stepY;
        }

        return false;
    }
}