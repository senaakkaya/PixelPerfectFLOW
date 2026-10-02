using UnityEngine;

[RequireComponent(typeof(ConveyorMover))]
public sealed class ShooterController : MonoBehaviour
{
    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    [Header("Tahta")]
    [SerializeField] private BoardManager board;
    [SerializeField] private BoardRenderer boardRenderer;

    [Header("Shooter")]
    [SerializeField, Min(0)] private int colorId = 3;
    [SerializeField, Min(1)] private int startingAmmo = 100;

    [Header("Görsel")]
    [SerializeField] private MeshRenderer visualRenderer;

    private ConveyorMover mover;
    private ShooterState state;

    private Material originalMaterial;
    private Material runtimeMaterial;

    public int RemainingAmmo =>
        state != null ? state.RemainingAmmo : 0;

    public bool IsReady => state != null;

    private void OnEnable()
    {
        mover = GetComponent<ConveyorMover>();
        mover.ShotOpportunity += HandleShotOpportunity;
    }

    public void Configure(
        BoardManager runtimeBoard,
        BoardRenderer renderer,
        int shooterColorId,
        int ammo)
    {
        if (IsReady)
        {
            Debug.LogError(
                "ShooterController: Configure, ilk Start çalışmadan " +
                "önce çağrılmalı.",
                this);
            return;
        }

        board = runtimeBoard;
        boardRenderer = renderer;
        colorId = shooterColorId;
        startingAmmo = ammo;
    }

    private void Start()
    {
        if (board == null ||
            boardRenderer == null ||
            !board.IsReady ||
            boardRenderer.Level == null)
        {
            Debug.LogError(
                "ShooterController: Tahta bağlantılarını kontrol et.",
                this);

            enabled = false;
            return;
        }

        ColorPalette palette = boardRenderer.Level.Palette;

        if (palette == null || !palette.Contains(colorId))
        {
            Debug.LogError(
                "ShooterController: Color Id palette bulunmuyor.",
                this);

            enabled = false;
            return;
        }

        if (startingAmmo <= 0)
        {
            Debug.LogError(
                "ShooterController: Başlangıç mühimmatı pozitif olmalı.",
                this);

            enabled = false;
            return;
        }

        state = new ShooterState(colorId, startingAmmo);

        SetVisualColor(palette.GetColor(state.ColorId));
    }

    private void HandleShotOpportunity(
        int startX,
        int startY,
        int stepX,
        int stepY)
    {
        if (!IsReady ||
            !state.HasAmmo ||
            board.RemainingBlocks <= 0)
        {
            return;
        }

        if (!board.TryFindFirstBlock(
                startX,
                startY,
                stepX,
                stepY,
                out int hitX,
                out int hitY,
                out int targetColor))
        {
            return;
        }

        if (targetColor != state.ColorId)
            return;

        if (board.TryRemoveBlock(hitX, hitY, state.ColorId))
            state.TryConsumeAmmo();
    }

    public ShooterState CreateSnapshot()
    {
        return state?.Clone();
    }

    private void SetVisualColor(Color color)
    {
        if (visualRenderer == null ||
            visualRenderer.sharedMaterial == null)
        {
            return;
        }

        originalMaterial = visualRenderer.sharedMaterial;
        runtimeMaterial = new Material(originalMaterial);

        if (runtimeMaterial.HasProperty(BaseColorId))
            runtimeMaterial.SetColor(BaseColorId, color);

        visualRenderer.sharedMaterial = runtimeMaterial;
    }

    private void OnDisable()
    {
        if (mover != null)
            mover.ShotOpportunity -= HandleShotOpportunity;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
            return;

        if (visualRenderer != null)
            visualRenderer.sharedMaterial = originalMaterial;

        Destroy(runtimeMaterial);
    }

    [ContextMenu("Test/Mühimmat Kopyasını Kontrol Et")]
    private void TestSnapshot()
    {
        if (!Application.isPlaying || !IsReady)
            return;

        int originalAmmo = state.RemainingAmmo;
        ShooterState copy = state.Clone();

        bool consumed = copy.TryConsumeAmmo();

        int expectedAmmo = originalAmmo > 0
            ? originalAmmo - 1
            : 0;

        bool passed =
            consumed == (originalAmmo > 0) &&
            copy.RemainingAmmo == expectedAmmo &&
            copy.ColorId == state.ColorId &&
            copy.InitialAmmo == state.InitialAmmo &&
            state.RemainingAmmo == originalAmmo;

        if (passed)
        {
            Debug.Log(
                "Mühimmat kopya testi geçti. " +
                "Asıl shooter'ın mühimmatı değişmedi.",
                this);
        }
        else
        {
            Debug.LogError("Mühimmat kopya testi başarısız.", this);
        }
    }
}