using UnityEngine;

// Normal Start metotlarından önce shooter listesini hazırlar.
// Sahnedeki Awake metotları bu noktada tamamlanmış olur.
[DefaultExecutionOrder(-50)]
public sealed class ShooterSpawner : MonoBehaviour
{
    [Header("Kaynak")]
    [SerializeField] private GameObject shooterPrefab;
    [SerializeField] private Transform shooterParent;

    [Header("Sistemler")]
    [SerializeField] private BoardManager board;
    [SerializeField] private BoardRenderer boardRenderer;
    [SerializeField] private ConveyorPath path;
    [SerializeField] private ShooterQueue queue;
    [SerializeField] private WaitingAreaManager waitingArea;
    [SerializeField] private GameManager gameManager;

    private void Start()
    {
        if (!ValidateSetup())
        {
            enabled = false;
            return;
        }

        LevelData level = boardRenderer.Level;

        ConveyorMover[] generated =
            new ConveyorMover[level.ShooterCount];

        for (int i = 0; i < generated.Length; i++)
        {
            ShooterData data = level.GetShooter(i);

            GameObject instance = Instantiate(
                shooterPrefab, shooterParent);

            instance.name = $"Shooter_{i:00}_Color_{data.ColorId}";

            ConveyorMover mover =
                instance.GetComponent<ConveyorMover>();

            ShooterController shooter =
                instance.GetComponent<ShooterController>();

            mover.Configure(path);

            shooter.Configure(
                board,
                boardRenderer,
                data.ColorId,
                data.Ammo);

            waitingArea.RegisterShooter(mover);
            generated[i] = mover;
        }

        queue.SetShooters(generated);
        gameManager.SetShooters(generated);
    }

    private bool ValidateSetup()
    {
        if (shooterPrefab == null ||
            shooterParent == null ||
            board == null ||
            boardRenderer == null ||
            path == null ||
            queue == null ||
            waitingArea == null ||
            gameManager == null)
        {
            Debug.LogError(
                "ShooterSpawner: Tüm Inspector alanlarını doldur.",
                this);
            return false;
        }

        if (!shooterParent.gameObject.activeInHierarchy ||
            !shooterPrefab.activeSelf)
        {
            Debug.LogError(
                "Shooter prefabı ve üretim üst nesnesi aktif olmalı.",
                this);
            return false;
        }

        if (!shooterPrefab.TryGetComponent(out ConveyorMover mover) ||
            !shooterPrefab.TryGetComponent(out ShooterController controller))
        {
            Debug.LogError(
                "Prefabın kökünde ConveyorMover ve ShooterController olmalı.",
                this);
            return false;
        }

        if (!mover.enabled || !controller.enabled)
        {
            Debug.LogError(
                "Prefab üzerindeki hareket ve shooter bileşenleri açık olmalı.",
                this);
            return false;
        }

        if (!board.IsReady || !waitingArea.IsReady || path.Length <= 0f)
        {
            Debug.LogError(
                "Tahta, bekleme alanı veya yol hazır değil. " +
                "Console'daki önceki hataları kontrol et.",
                this);
            return false;
        }

        LevelData level = boardRenderer.Level;

        if (level == null ||
            level.Palette == null ||
            level.ShooterCount == 0)
        {
            Debug.LogError(
                "Bölümün paleti veya shooter listesi eksik.",
                this);
            return false;
        }

        for (int i = 0; i < level.ShooterCount; i++)
        {
            ShooterData data = level.GetShooter(i);

            if (!level.Palette.Contains(data.ColorId) || data.Ammo <= 0)
            {
                Debug.LogError(
                    $"Bölümdeki {i}. shooter'ın rengi veya mühimmatı geçersiz.",
                    this);
                return false;
            }
        }

        return true;
    }
}