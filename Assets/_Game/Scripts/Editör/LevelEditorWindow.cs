using UnityEditor;
using UnityEngine;

public sealed class LevelEditorWindow : EditorWindow
{
    [SerializeField] private LevelData level;

    [SerializeField] private int selectedColor;
    [SerializeField] private bool eraser;
    [SerializeField] private int newWidth = 32;
    [SerializeField] private int newHeight = 32;
    [SerializeField] private float cellSize = 16f;

    private Vector2 scroll;

    private bool painting;
    private int undoGroup = -1;
    private Vector2Int lastCell;
    private bool hasLastCell;

    [MenuItem("Tools/ColorLoop/Level Editor")]
    private static void Open()
    {
        LevelEditorWindow window =
            GetWindow<LevelEditorWindow>("Level Editor");

        window.minSize = new Vector2(420f, 400f);
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        EndStroke();
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    private void OnLostFocus()
    {
        EndStroke();
    }

    private void OnUndoRedo()
    {
        painting = false;
        hasLastCell = false;
        undoGroup = -1;
        Repaint();
    }

    private void OnGUI()
    {
        Event current = Event.current;

        if (painting && current.rawType == EventType.MouseUp)
            EndStroke();

        LevelData chosenLevel = (LevelData)EditorGUILayout.ObjectField(
            "Bölüm", level, typeof(LevelData), false);

        if (chosenLevel != level)
        {
            EndStroke();
            level = chosenLevel;
            scroll = Vector2.zero;
        }

        if (level == null)
        {
            EditorGUILayout.HelpBox(
                "Düzenlemek istediğin LevelData dosyasını seç.",
                MessageType.Info);
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EndStroke();

            EditorGUILayout.HelpBox(
                "Bölümü düzenlemek için Play modunu durdur.",
                MessageType.Info);
            return;
        }

        if (level.Palette == null || level.Palette.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Bölüm dosyasına dolu bir renk paleti ata.",
                MessageType.Warning);
            return;
        }

        selectedColor = Mathf.Clamp(
            selectedColor, 0, level.Palette.Count - 1);

        DrawFileControls();
        DrawCanvasControls();
        DrawPalette();

        EditorGUILayout.Space(4f);

        eraser = GUILayout.Toggle(
            eraser, "Silgi", "Button", GUILayout.Height(24f));

        cellSize = EditorGUILayout.Slider(
            "Yakınlaştırma", cellSize, 6f, 32f);

        EditorGUILayout.HelpBox(
            "Sol tuş: boya • Basılı tutup sürükle: çiz\n" +
            "Sağ tuş: sil • Silgi açıkken sol tuş da siler\n" +
            "Bir sürükleme tek geri alma adımıdır.",
            MessageType.Info);

        if (level.Width < 1 || level.Width > 128 ||
            level.Height < 1 || level.Height > 128)
        {
            EditorGUILayout.HelpBox(
                "Bu editör 1–128 arasındaki tahta boyutlarını destekliyor.",
                MessageType.Warning);
            return;
        }

        if (!level.HasBoardData)
        {
            EditorGUILayout.HelpBox(
                "Önce boş tuval oluştur veya PNG içe aktar.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField(
            $"Tahta: {level.Width} × {level.Height}");

        DrawCanvas();
    }

    private void DrawFileControls()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("PNG'den İçe Aktar"))
        {
            EndStroke();
            PixelArtImporter.Import(level);
            Repaint();
        }

        if (GUILayout.Button("Kaydet"))
        {
            EndStroke();
            AssetDatabase.SaveAssets();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Geri Al"))
        {
            EndStroke();
            Undo.PerformUndo();
        }

        if (GUILayout.Button("Yinele"))
        {
            EndStroke();
            Undo.PerformRedo();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawCanvasControls()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "Yeni Tuval", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        newWidth = Mathf.Clamp(
            EditorGUILayout.IntField("Genişlik", newWidth), 1, 128);

        newHeight = Mathf.Clamp(
            EditorGUILayout.IntField("Yükseklik", newHeight), 1, 128);

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Boş Tuval Oluştur"))
        {
            EndStroke();
            CreateEmptyCanvas();
        }

        if (GUILayout.Button("Boş Kenarları Kırp"))
        {
            EndStroke();
            LevelCropper.Crop(level);
            scroll = Vector2.zero;
            Repaint();
        }

        if (GUILayout.Button("Bölümü Kontrol Et"))
        {
            EndStroke();
            LevelValidator.Validate(level);
        }
    }

    private void DrawPalette()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "Renk Paleti", EditorStyles.boldLabel);

        int count = level.Palette.Count;
        int columns = Mathf.Max(
            1, Mathf.FloorToInt((position.width - 24f) / 52f));

        Color oldBackground = GUI.backgroundColor;

        for (int start = 0; start < count; start += columns)
        {
            EditorGUILayout.BeginHorizontal();

            int end = Mathf.Min(start + columns, count);

            for (int i = start; i < end; i++)
            {
                GUI.backgroundColor = level.Palette.GetColor(i);

                string label = selectedColor == i && !eraser
                    ? $"[{i}]"
                    : i.ToString();

                if (GUILayout.Button(
                        label,
                        GUILayout.Width(46f),
                        GUILayout.Height(28f)))
                {
                    selectedColor = i;
                    eraser = false;
                }
            }

            GUI.backgroundColor = oldBackground;
            EditorGUILayout.EndHorizontal();
        }

        GUI.backgroundColor = oldBackground;
    }

    private void CreateEmptyCanvas()
    {
        if (level.CellCount > 0 &&
            !EditorUtility.DisplayDialog(
                "Boş tuval oluştur",
                "Mevcut çizim ve shooter listesi temizlenecek. " +
                "İşlem geri alınabilir.",
                "Oluştur",
                "Vazgeç"))
        {
            return;
        }

        using (SerializedObject data = new SerializedObject(level))
        {
            data.Update();

            data.FindProperty("width").intValue = newWidth;
            data.FindProperty("height").intValue = newHeight;

            SerializedProperty cells = data.FindProperty("cells");
            cells.arraySize = newWidth * newHeight;

            for (int i = 0; i < cells.arraySize; i++)
            {
                cells.GetArrayElementAtIndex(i).intValue =
                    LevelData.EmptyCell;
            }

            data.FindProperty("shooters").arraySize = 0;
            data.ApplyModifiedProperties();
        }

        scroll = Vector2.zero;
        Repaint();
    }

    private void DrawCanvas()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        Rect canvas = GUILayoutUtility.GetRect(
            level.Width * cellSize,
            level.Height * cellSize,
            GUILayout.ExpandWidth(false),
            GUILayout.ExpandHeight(false));

        Event current = Event.current;

        if (current.type == EventType.Repaint)
        {
            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    int colorId = level.GetCell(x, y);

                    Color color;

                    if (colorId == LevelData.EmptyCell)
                    {
                        float shade = (x + y) % 2 == 0 ? 0.20f : 0.27f;
                        color = new Color(shade, shade, shade);
                    }
                    else
                    {
                        color = level.Palette.Contains(colorId)
                            ? level.Palette.GetColor(colorId)
                            : Color.magenta;
                    }

                    Rect cell = new Rect(
                        canvas.x + x * cellSize,
                        canvas.y + (level.Height - 1 - y) * cellSize,
                        cellSize - 1f,
                        cellSize - 1f);

                    EditorGUI.DrawRect(cell, color);
                }
            }
        }

        HandlePainting(canvas, current);

        EditorGUILayout.EndScrollView();
    }

    private void HandlePainting(Rect canvas, Event current)
    {
        bool mouseDown = current.type == EventType.MouseDown;
        bool mouseDrag = current.type == EventType.MouseDrag;

        if (!mouseDown && !mouseDrag)
            return;

        if (current.button != 0 && current.button != 1)
            return;

        if (!canvas.Contains(current.mousePosition))
        {
            hasLastCell = false;
            return;
        }

        if (mouseDown)
        {
            EndStroke();

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bölümü Boya");

            painting = true;
            hasLastCell = false;
        }

        if (!painting)
            return;

        int x = Mathf.FloorToInt(
            (current.mousePosition.x - canvas.x) / cellSize);

        int screenY = Mathf.FloorToInt(
            (current.mousePosition.y - canvas.y) / cellSize);

        int y = level.Height - 1 - screenY;

        x = Mathf.Clamp(x, 0, level.Width - 1);
        y = Mathf.Clamp(y, 0, level.Height - 1);

        Vector2Int cell = new Vector2Int(x, y);

        int colorId = eraser || current.button == 1
            ? LevelData.EmptyCell
            : selectedColor;

        if (!hasLastCell || cell != lastCell)
        {
            PaintLine(
                hasLastCell ? lastCell : cell,
                cell,
                colorId);
        }

        lastCell = cell;
        hasLastCell = true;

        current.Use();
        Repaint();
    }

    private void PaintLine(Vector2Int from, Vector2Int to, int colorId)
    {
        using (SerializedObject data = new SerializedObject(level))
        {
            data.Update();

            SerializedProperty cells = data.FindProperty("cells");

            int x = from.x;
            int y = from.y;

            int dx = Mathf.Abs(to.x - x);
            int dy = Mathf.Abs(to.y - y);

            int stepX = x < to.x ? 1 : -1;
            int stepY = y < to.y ? 1 : -1;
            int error = dx - dy;

            bool changed = false;

            // Hızlı sürüklemede aradaki hücrelerin atlanmasını önler.
            while (true)
            {
                int index = y * level.Width + x;
                SerializedProperty cell =
                    cells.GetArrayElementAtIndex(index);

                if (cell.intValue != colorId)
                {
                    cell.intValue = colorId;
                    changed = true;
                }

                if (x == to.x && y == to.y)
                    break;

                int doubledError = error * 2;

                if (doubledError > -dy)
                {
                    error -= dy;
                    x += stepX;
                }

                if (doubledError < dx)
                {
                    error += dx;
                    y += stepY;
                }
            }

            if (changed)
            {
                // Eski shooter çözümü değişen tahtaya ait olmayabilir.
                data.FindProperty("shooters").arraySize = 0;
                data.ApplyModifiedProperties();
            }
        }
    }

    private void EndStroke()
    {
        if (!painting)
            return;

        if (undoGroup >= 0)
            Undo.CollapseUndoOperations(undoGroup);

        painting = false;
        hasLastCell = false;
        undoGroup = -1;
    }
}