using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class BoardFrameFitter : MonoBehaviour
{
    [SerializeField] private BoardRenderer board;

    [Header("Köşe Noktaları")]
    [SerializeField] private Transform cornerA;
    [SerializeField] private Transform cornerB;
    [SerializeField] private Transform cornerC;
    [SerializeField] private Transform cornerD;

    [Header("Tahtanın Her Kenarındaki Bant Payı")]
    [SerializeField, Min(0f)] private float beltMargin = 0.6f;

    private void OnEnable()
    {
        RefreshFrame();
    }

    [ContextMenu("Refresh Frame")]
    public void RefreshFrame()
    {
        if (board == null || board.Level == null ||
            cornerA == null || cornerB == null ||
            cornerC == null || cornerD == null)
        {
            Debug.LogWarning(
                "BoardFrameFitter: Board ve dört köşe atanmalı.",
                this);
            return;
        }

        LevelData level = board.Level;

        if (level.Width < 1 || level.Height < 1)
            return;

        float margin = Mathf.Max(0f, beltMargin);

        float halfWidth =
            level.Width * board.CellSize * 0.5f + margin;

        float halfDepth =
            level.Height * board.CellSize * 0.5f + margin;

        SetCorner(cornerA, -halfWidth, -halfDepth);
        SetCorner(cornerB,  halfWidth, -halfDepth);
        SetCorner(cornerC, -halfWidth,  halfDepth);
        SetCorner(cornerD,  halfWidth,  halfDepth);
    }

    private void SetCorner(Transform corner, float x, float z)
    {
        corner.position = board.transform.TransformPoint(
            new Vector3(x, 0f, z));
    }
}