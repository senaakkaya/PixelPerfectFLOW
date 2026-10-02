using UnityEngine;
using UnityEngine.Rendering;

public sealed class BoardRenderer : MonoBehaviour
{
    private const int BatchSize = 256;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    [Header("Kaynaklar")]
    [SerializeField] private LevelData level;
    [SerializeField] private Mesh cubeMesh;
    [SerializeField] private Material materialTemplate;
    [SerializeField] private BoardManager boardManager;

    [Header("Yerleşim")]
    [SerializeField, Min(0.01f)] private float cellSize = 0.15f;
    [SerializeField, Range(0.1f, 1f)] private float fillRatio = 1f;

    public LevelData Level =>
        Application.isPlaying && LevelSession.CurrentLevel != null
            ? LevelSession.CurrentLevel
            : level;
    public float CellSize => GameTuning.Load().CellSize;

    private Matrix4x4[][] matricesByColor;
    private int[][] cellIndicesByColor;
    private int[] activeCounts;

    private int[] colorByCell;
    private int[] slotByCell;

    private Material[] runtimeMaterials;
    private RenderParams[] renderParams;

    private bool isBuilt;

    private void OnEnable()
    {
        cellSize = GameTuning.Load().CellSize;
        if (LevelSession.CurrentLevel != null)
            level = LevelSession.CurrentLevel;
        if (boardManager != null)
            boardManager.BlockRemoved += HandleBlockRemoved;

        // İlk açılışta BoardManager'ın Awake metodunu bekle.
        // Sonraki etkinleştirmelerde güncel durumdan yeniden kur.
        if (boardManager != null && boardManager.IsReady)
            TryBuildBoard();
    }

    private void Start()
    {
        if (!isBuilt)
            TryBuildBoard();
    }

    private void TryBuildBoard()
    {
        if (!ValidateSetup())
        {
            enabled = false;
            return;
        }

        BuildBoard();
        isBuilt = true;
    }

    private bool ValidateSetup()
    {
        if (level == null ||
            cubeMesh == null ||
            materialTemplate == null ||
            boardManager == null)
        {
            Debug.LogError(
                "BoardRenderer: Tüm kaynak alanlarını doldur.",
                this);
            return false;
        }

        if (!boardManager.IsReady ||
            boardManager.Width != level.Width ||
            boardManager.Height != level.Height)
        {
            Debug.LogError(
                "BoardRenderer: BoardManager verisi hazır değil " +
                "veya bölüm boyutları uyuşmuyor.",
                this);
            return false;
        }

        if (level.Palette == null || level.Palette.Count == 0)
        {
            Debug.LogError(
                "BoardRenderer: Renk paleti eksik.",
                this);
            return false;
        }

        if (!SystemInfo.supportsInstancing ||
            !materialTemplate.enableInstancing)
        {
            Debug.LogError(
                "GPU instancing desteğini ve materyal ayarını kontrol et.",
                this);
            return false;
        }

        if (!materialTemplate.HasProperty(BaseColorId))
        {
            Debug.LogError(
                "Materyal için Universal Render Pipeline/Lit kullan.",
                this);
            return false;
        }

        for (int y = 0; y < boardManager.Height; y++)
        {
            for (int x = 0; x < boardManager.Width; x++)
            {
                int colorId = boardManager.GetColor(x, y);

                if (colorId != LevelData.EmptyCell &&
                    !level.Palette.Contains(colorId))
                {
                    Debug.LogError(
                        $"Geçersiz renk kimliği: {colorId}",
                        this);
                    return false;
                }
            }
        }

        return true;
    }

    private void BuildBoard()
    {
        int colorCount = level.Palette.Count;
        int totalCells = boardManager.Width * boardManager.Height;

        activeCounts = new int[colorCount];
        colorByCell = new int[totalCells];
        slotByCell = new int[totalCells];

        // Renklerin ihtiyaç duyduğu kapasiteyi hesapla.
        for (int y = 0; y < boardManager.Height; y++)
        {
            for (int x = 0; x < boardManager.Width; x++)
            {
                int index = y * boardManager.Width + x;
                int colorId = boardManager.GetColor(x, y);

                colorByCell[index] = colorId;
                slotByCell[index] = -1;

                if (colorId != LevelData.EmptyCell)
                    activeCounts[colorId]++;
            }
        }

        matricesByColor = new Matrix4x4[colorCount][];
        cellIndicesByColor = new int[colorCount][];
        runtimeMaterials = new Material[colorCount];
        renderParams = new RenderParams[colorCount];

        for (int color = 0; color < colorCount; color++)
        {
            int count = activeCounts[color];

            matricesByColor[color] = new Matrix4x4[count];
            cellIndicesByColor[color] = new int[count];

            if (count == 0)
                continue;

            Material material = new Material(materialTemplate);
            material.SetColor(
                BaseColorId, level.Palette.GetColor(color));

            runtimeMaterials[color] = material;

            renderParams[color] = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                layer = gameObject.layer
            };
        }

        float spacing = CellSize;
        float size = spacing * Mathf.Clamp(fillRatio, 0.1f, 1f);

        float offsetX = (boardManager.Width - 1) * spacing * 0.5f;
        float offsetZ = (boardManager.Height - 1) * spacing * 0.5f;

        Vector3 scale = Vector3.one * size;
        Matrix4x4 boardToWorld = transform.localToWorldMatrix;

        int[] writeIndices = new int[colorCount];

        for (int y = 0; y < boardManager.Height; y++)
        {
            for (int x = 0; x < boardManager.Width; x++)
            {
                int index = y * boardManager.Width + x;
                int colorId = colorByCell[index];

                if (colorId == LevelData.EmptyCell)
                    continue;

                int slot = writeIndices[colorId]++;

                Vector3 position = new Vector3(
                    x * spacing - offsetX,
                    0f,
                    y * spacing - offsetZ);

                matricesByColor[colorId][slot] =
                    boardToWorld * Matrix4x4.TRS(
                        position, Quaternion.identity, scale);

                cellIndicesByColor[colorId][slot] = index;
                slotByCell[index] = slot;
            }
        }
    }

    private void HandleBlockRemoved(int cellIndex)
    {
        if (!isBuilt)
            return;

        if (cellIndex < 0 || cellIndex >= slotByCell.Length)
            return;

        int slot = slotByCell[cellIndex];

        if (slot < 0)
            return;

        int color = colorByCell[cellIndex];
        int lastSlot = activeCounts[color] - 1;

        // Grubun son küpünü kaldırılan küpün yerine taşı.
        if (slot != lastSlot)
        {
            matricesByColor[color][slot] =
                matricesByColor[color][lastSlot];

            int movedCell = cellIndicesByColor[color][lastSlot];

            cellIndicesByColor[color][slot] = movedCell;
            slotByCell[movedCell] = slot;
        }

        activeCounts[color]--;

        slotByCell[cellIndex] = -1;
        colorByCell[cellIndex] = LevelData.EmptyCell;
    }

    private void Update()
    {
        if (!isBuilt)
            return;

        for (int color = 0; color < activeCounts.Length; color++)
        {
            int activeCount = activeCounts[color];

            for (int start = 0; start < activeCount; start += BatchSize)
            {
                int count = Mathf.Min(
                    BatchSize, activeCount - start);

                Graphics.RenderMeshInstanced(
                    renderParams[color],
                    cubeMesh,
                    0,
                    matricesByColor[color],
                    count,
                    start);
            }
        }
    }

    private void OnDisable()
    {
        if (boardManager != null)
            boardManager.BlockRemoved -= HandleBlockRemoved;

        isBuilt = false;

        if (runtimeMaterials != null)
        {
            for (int i = 0; i < runtimeMaterials.Length; i++)
            {
                if (runtimeMaterials[i] != null)
                    Destroy(runtimeMaterials[i]);
            }
        }

        matricesByColor = null;
        cellIndicesByColor = null;
        activeCounts = null;
        colorByCell = null;
        slotByCell = null;
        runtimeMaterials = null;
        renderParams = null;
    }
}