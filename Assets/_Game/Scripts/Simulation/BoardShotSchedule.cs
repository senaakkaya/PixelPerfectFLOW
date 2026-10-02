using System;

public static class BoardShotSchedule
{
    public static int GetPointCount(int width, int height)
    {
        return 2 * (width + height);
    }

    public static void GetPoint(
        int width,
        int height,
        double cellSize,
        double margin,
        int index,
        out double distance,
        out int startX,
        out int startY,
        out int stepX,
        out int stepY)
    {
        int pointCount = GetPointCount(width, height);

        if (index < 0 || index >= pointCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        double pathWidth = width * cellSize + 2d * margin;
        double pathDepth = height * cellSize + 2d * margin;

        // Alt kenar: soldan sağa.
        if (index < width)
        {
            distance = margin + (index + 0.5d) * cellSize;

            startX = index;
            startY = 0;
            stepX = 0;
            stepY = 1;
            return;
        }

        index -= width;

        // Sağ kenar: aşağıdan yukarıya.
        if (index < height)
        {
            distance =
                pathWidth + margin + (index + 0.5d) * cellSize;

            startX = width - 1;
            startY = index;
            stepX = -1;
            stepY = 0;
            return;
        }

        index -= height;

        // Üst kenar: sağdan sola.
        if (index < width)
        {
            distance =
                pathWidth + pathDepth +
                margin + (index + 0.5d) * cellSize;

            startX = width - 1 - index;
            startY = height - 1;
            stepX = 0;
            stepY = -1;
            return;
        }

        index -= width;

        // Sol kenar: yukarıdan aşağıya.
        distance =
            2d * pathWidth + pathDepth +
            margin + (index + 0.5d) * cellSize;

        startX = 0;
        startY = height - 1 - index;
        stepX = 1;
        stepY = 0;
    }
}