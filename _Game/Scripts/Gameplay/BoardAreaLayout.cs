using UnityEngine;

// ShooterSpawner ve ShooterQueue'nun Start metotlarından önce çalışır.
[DefaultExecutionOrder(-100)]
public sealed class BoardAreaLayout : MonoBehaviour
{
    [Header("Bağlantılar")]
    [SerializeField] private BoardRenderer board;
    [SerializeField] private Transform waitingArea;
    [SerializeField] private Transform shooterQueue;

    [Header("Boşluklar")]
    [SerializeField, Min(0.1f)]
    private float waitingDistanceFromPath = 0.8f;

    [SerializeField, Min(0.1f)]
    private float queueDistanceFromWaiting = 0.9f;

    [Header("Kuyruk")]
    [Tooltip("Üç sütun arasında 0.6 mesafe varsa -0.6 kullan.")]
    [SerializeField]
    private float firstLaneX = -0.6f;

    [SerializeField]
    private float queueHeight = 0.15f;

    private void Start()
    {
        if (board == null ||
            board.Level == null ||
            waitingArea == null ||
            shooterQueue == null)
        {
            Debug.LogError(
                "BoardAreaLayout: Board, Waiting Area ve " +
                "Shooter Queue bağlantılarını tamamla.",
                this);

            return;
        }

        GameTuning tuning = GameTuning.Load();

        float halfBoardDepth =
            board.Level.Height * tuning.CellSize * 0.5f;

        // Tahtanın alt kenarının dışındaki bant.
        float bottomPathZ =
            -halfBoardDepth - tuning.DistanceFromBoard;

        // Önce bekleme alanı, onun altında kuyruk.
        float waitingZ =
            bottomPathZ - waitingDistanceFromPath;

        float queueZ =
            waitingZ - queueDistanceFromWaiting;

        Transform boardTransform = board.transform;

        // Slotların kendi yerel yükseklikleri korunur.
        waitingArea.SetPositionAndRotation(
            boardTransform.TransformPoint(
                new Vector3(0f, 0f, waitingZ)),
            boardTransform.rotation);

        shooterQueue.SetPositionAndRotation(
            boardTransform.TransformPoint(
                new Vector3(firstLaneX, queueHeight, queueZ)),
            boardTransform.rotation);
    }
}