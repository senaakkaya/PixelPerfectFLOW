using System;
using UnityEngine;

public sealed class ConveyorPath : MonoBehaviour
{
    [SerializeField] private BoardRenderer board;

    [Header("Yol Ayarları")]
    [SerializeField, Min(0.01f)]
    private float distanceFromBoard = 0.7f;

    [SerializeField]
    private float pathHeight = 0.15f;

    private int columns;
    private int rows;

    private double spacing;
    private double margin;
    private double pathWidth;
    private double pathDepth;
    private double pathLength;

    private bool isReady;

    public float Length => isReady ? (float)pathLength : 0f;

    public int ShotPointCount => isReady
        ? BoardShotSchedule.GetPointCount(columns, rows)
        : 0;

    private void Awake()
    {
        RebuildPath();
    }

    public void RebuildPath()
    {
        distanceFromBoard = GameTuning.Load().DistanceFromBoard;
        isReady = false;

        if (board == null || board.Level == null)
        {
            Debug.LogError(
                "ConveyorPath: Board ve bölüm bağlantısı gerekli.",
                this);

            return;
        }

        if (!board.Level.HasBoardData)
        {
            Debug.LogError(
                "ConveyorPath: Bölümün tahta verisi geçersiz.",
                this);

            return;
        }

        if (float.IsNaN(distanceFromBoard) ||
            float.IsInfinity(distanceFromBoard) ||
            distanceFromBoard <= 0f)
        {
            Debug.LogError(
                "ConveyorPath: Tahtadan uzaklık sıfırdan büyük olmalı.",
                this);

            return;
        }

        columns = board.Level.Width;
        rows = board.Level.Height;

        spacing = board.CellSize;
        margin = distanceFromBoard;

        pathWidth = columns * spacing + 2d * margin;
        pathDepth = rows * spacing + 2d * margin;
        pathLength = 2d * (pathWidth + pathDepth);

        isReady = true;
    }

    public void GetShotPoint(
        int index,
        out float distance,
        out int startX,
        out int startY,
        out int stepX,
        out int stepY)
    {
        if (!isReady)
        {
            throw new InvalidOperationException(
                "ConveyorPath henüz hazır değil.");
        }

        BoardShotSchedule.GetPoint(
            columns,
            rows,
            spacing,
            margin,
            index,
            out double preciseDistance,
            out startX,
            out startY,
            out stepX,
            out stepY);

        distance = (float)preciseDistance;
    }

    public bool TryGetPose(
        float distance,
        out Vector3 position,
        out Vector3 direction)
    {
        position = default;
        direction = Vector3.forward;

        if (!isReady)
            return false;

        double d = Mathf.Repeat(distance, (float)pathLength);

        double halfWidth = pathWidth * 0.5d;
        double halfDepth = pathDepth * 0.5d;

        Vector3 localPosition;
        Vector3 localDirection;

        if (d < pathWidth)
        {
            localPosition = new Vector3(
                (float)(-halfWidth + d),
                pathHeight,
                (float)-halfDepth);

            localDirection = Vector3.right;
        }
        else if (d < pathWidth + pathDepth)
        {
            localPosition = new Vector3(
                (float)halfWidth,
                pathHeight,
                (float)(-halfDepth + d - pathWidth));

            localDirection = Vector3.forward;
        }
        else if (d < 2d * pathWidth + pathDepth)
        {
            localPosition = new Vector3(
                (float)(halfWidth - (d - pathWidth - pathDepth)),
                pathHeight,
                (float)halfDepth);

            localDirection = Vector3.left;
        }
        else
        {
            localPosition = new Vector3(
                (float)-halfWidth,
                pathHeight,
                (float)(halfDepth -
                    (d - 2d * pathWidth - pathDepth)));

            localDirection = Vector3.back;
        }

        position = board.transform.TransformPoint(localPosition);

        direction = board.transform
            .TransformDirection(localDirection)
            .normalized;

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (board == null || board.Level == null)
            return;

        float halfWidth =
            board.Level.Width * board.CellSize * 0.5f +
            distanceFromBoard;

        float halfDepth =
            board.Level.Height * board.CellSize * 0.5f +
            distanceFromBoard;

        Vector3 a = board.transform.TransformPoint(
            new Vector3(-halfWidth, pathHeight, -halfDepth));

        Vector3 b = board.transform.TransformPoint(
            new Vector3(halfWidth, pathHeight, -halfDepth));

        Vector3 c = board.transform.TransformPoint(
            new Vector3(halfWidth, pathHeight, halfDepth));

        Vector3 d = board.transform.TransformPoint(
            new Vector3(-halfWidth, pathHeight, halfDepth));

        Color previousColor = Gizmos.color;

        Gizmos.color = Color.cyan;

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);

        Gizmos.color = previousColor;
    }
}