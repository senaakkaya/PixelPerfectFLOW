using System;

public static class PixelPatternGenerator
{
    public static int[] Generate(
        int width,
        int height,
        int colorCount,
        int regionCount,
        int seed,
        bool ovalShape,
        int minimumColorCount = 1)
    {
        if (width < 1 || width > 128)
            throw new ArgumentOutOfRangeException(nameof(width));

        if (height < 1 || height > 128)
            throw new ArgumentOutOfRangeException(nameof(height));

        if (colorCount < 1)
            throw new ArgumentOutOfRangeException(nameof(colorCount));

        if (minimumColorCount < 1 ||
            minimumColorCount > colorCount)
        {
            throw new ArgumentException(
                "En az renk sayısı, 1 ile en fazla renk sayısı arasında olmalı.");
        }

        if (regionCount < 1 || regionCount > 64)
            throw new ArgumentOutOfRangeException(nameof(regionCount));

        var random = new Random(seed);

        int halfWidth = (width + 1) / 2;

        // Yalnızca şeklin içinde kalan sol yarı hücreleri.
        int[] candidates = new int[halfWidth * height];
        int candidateCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < halfWidth; x++)
            {
                if (ovalShape && !IsInsideOval(x, y, width, height))
                    continue;

                candidates[candidateCount++] = y * width + x;
            }
        }

        int possibleColorCount = Math.Min(
            colorCount,
            Math.Min(candidateCount, 64));

        if (minimumColorCount > possibleColorCount)
        {
            throw new InvalidOperationException(
                "Bu boyut ve şekil, istenen minimum renk sayısı için " +
                "yeterli bağımsız bölge içermiyor. " +
                "Boyutu büyüt veya minimum renk sayısını azalt.");
        }

        int usedColorCount = random.Next(
            minimumColorCount,
            possibleColorCount + 1);

        // Her renk için en az bir bölge oluştur.
        int actualRegionCount = Math.Min(
            candidateCount,
            Math.Max(regionCount, usedColorCount));

        int[] regionX = new int[actualRegionCount];
        int[] regionY = new int[actualRegionCount];
        int[] regionColors = new int[actualRegionCount];

        // İlk colorCount renk arasından farklı renkler seç.
        int[] colorIds = new int[colorCount];

        for (int i = 0; i < colorIds.Length; i++)
            colorIds[i] = i;

        for (int i = 0; i < usedColorCount; i++)
        {
            int selected = random.Next(i, colorIds.Length);
            Swap(colorIds, i, selected);
        }

        // Merkezler farklı hücrelerde ve şeklin içinde olacak.
        // Böylece hiçbir renk bölgesi tamamen kaybolmayacak.
        for (int i = 0; i < actualRegionCount; i++)
        {
            int selected = random.Next(i, candidateCount);
            Swap(candidates, i, selected);

            int cellIndex = candidates[i];

            regionX[i] = cellIndex % width;
            regionY[i] = cellIndex / width;

            regionColors[i] = i < usedColorCount
                ? colorIds[i]
                : colorIds[random.Next(usedColorCount)];
        }

        int[] cells = new int[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;

                if (ovalShape && !IsInsideOval(x, y, width, height))
                {
                    cells[index] = BoardState.EmptyCell;
                    continue;
                }

                int mirroredX = Math.Min(x, width - 1 - x);

                int nearestRegion = 0;
                int nearestDistance = int.MaxValue;

                for (int region = 0; region < actualRegionCount; region++)
                {
                    int dx = mirroredX - regionX[region];
                    int dy = y - regionY[region];

                    int distance = dx * dx + dy * dy;

                    if (distance >= nearestDistance)
                        continue;

                    nearestDistance = distance;
                    nearestRegion = region;
                }

                cells[index] = regionColors[nearestRegion];
            }
        }

        return cells;
    }

    private static void Swap(int[] array, int a, int b)
    {
        int temporary = array[a];
        array[a] = array[b];
        array[b] = temporary;
    }

    private static bool IsInsideOval(
        int x,
        int y,
        int width,
        int height)
    {
        double normalizedX = (2d * x + 1d - width) / width;
        double normalizedY = (2d * y + 1d - height) / height;

        return normalizedX * normalizedX +
               normalizedY * normalizedY <= 1d;
    }
}