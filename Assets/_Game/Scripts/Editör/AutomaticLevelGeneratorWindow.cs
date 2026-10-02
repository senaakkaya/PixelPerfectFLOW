using System;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class AutomaticLevelGeneratorWindow : EditorWindow
{
    [SerializeField] private ColorPalette palette;

    [SerializeField] private int width = 24;
    [SerializeField] private int height = 24;

    [SerializeField] private int minimumColorCount = 3;
    [SerializeField] private int colorCount = 4;
    [SerializeField] private int regionCount = 10;

    [SerializeField] private int seed = 12345;
    [SerializeField] private bool ovalShape = true;

    [SerializeField] private int conveyorCapacity = 4;
    [SerializeField] private int waitingCapacity = 5;

    [SerializeField] private float cellSize = 0.15f;
    [SerializeField] private float distanceFromBoard = 0.7f;
    [SerializeField] private float entryClearance = 0.6f;
    [SerializeField] private float movementSpeed = 1.5f;

    [SerializeField] private int maxAmmoPerShooter = 30;
    [SerializeField] private int maxShooters = 512;
    [SerializeField] private int timeLimitMilliseconds = 3000;

    private Vector2 scroll;
    private string report;
    private MessageType reportType;

    [MenuItem("Tools/ColorLoop/Automatic Level Generator")]
    private static void Open()
    {
        var window = GetWindow<AutomaticLevelGeneratorWindow>(
            "Automatic Level Generator");

        window.minSize = new Vector2(440f, 580f);
        window.Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField(
            "Otomatik Bölüm Üret",
            EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        palette = (ColorPalette)EditorGUILayout.ObjectField(
            "Renk Paleti",
            palette,
            typeof(ColorPalette),
            false);

        width = EditorGUILayout.IntSlider(
            "Genişlik", width, 1, 128);

        height = EditorGUILayout.IntSlider(
            "Yükseklik", height, 1, 128);

        if (palette != null && palette.Count > 0)
        {
            colorCount = EditorGUILayout.IntSlider(
                "En Fazla Renk",
                colorCount,
                1,
                palette.Count);

            minimumColorCount = EditorGUILayout.IntSlider(
                "En Az Renk",
                minimumColorCount,
                1,
                colorCount);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Renk sınırlarını ayarlamak için bir palet seç.",
                MessageType.Info);
        }

        regionCount = EditorGUILayout.IntSlider(
            "Renk Bölgesi Sayısı",
            regionCount,
            1,
            64);

        ovalShape = EditorGUILayout.Toggle(
            "Oval Dış Sınır",
            ovalShape);

        seed = EditorGUILayout.IntField("Seed", seed);

        EditorGUILayout.HelpBox(
            "Örneğin en az 3, en fazla 4 seçersen desen 3 veya 4 " +
            "farklı renk içerir. İkisini de 4 yaparsan tam 4 renk kullanılır.\n\n" +
            "Renkler paletin ilk 'En Fazla Renk' adedi arasından seçilir. " +
            "Bölge sayısı gerekirse kullanılan renk sayısına yükseltilir.",
            MessageType.Info);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Bölüm Kuralları",
            EditorStyles.boldLabel);

        conveyorCapacity = EditorGUILayout.IntField(
            "Bant Kapasitesi",
            conveyorCapacity);

        waitingCapacity = EditorGUILayout.IntField(
            "Bekleme Kapasitesi",
            waitingCapacity);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Sahneyle Eşleşmesi Gereken Ayarlar",
            EditorStyles.boldLabel);

        GameTuningEditorFields.Draw(
            ref cellSize,
            ref distanceFromBoard,
            ref entryClearance,
            ref movementSpeed);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Shooter Üretimi",
            EditorStyles.boldLabel);

        maxAmmoPerShooter = EditorGUILayout.IntField(
            "Shooter Başına En Fazla Mermi",
            maxAmmoPerShooter);

        maxShooters = EditorGUILayout.IntField(
            "En Fazla Shooter",
            maxShooters);

        timeLimitMilliseconds = EditorGUILayout.IntField(
            "Süre Sınırı (ms)",
            timeLimitMilliseconds);

        if (EditorGUI.EndChangeCheck())
            report = null;

        bool playMode =
            EditorApplication.isPlayingOrWillChangePlaymode;

        if (playMode)
        {
            EditorGUILayout.HelpBox(
                "Üretim için Play modundan çık.",
                MessageType.Warning);
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(
                   palette == null || playMode))
        {
            if (GUILayout.Button(
                    "Yeni Bölüm Üret ve Kaydet",
                    GUILayout.Height(36f)))
            {
                GenerateAndSave();
            }
        }

        if (!string.IsNullOrEmpty(report))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(report, reportType);
        }

        EditorGUILayout.EndScrollView();
    }

    private void GenerateAndSave()
    {
        report = null;

        string assetPath = EditorUtility.SaveFilePanelInProject(
            "Yeni Bölümü Kaydet",
            "Level_Auto_002",
            "asset",
            "Yeni bölüm için dosya adı seç.");

        if (string.IsNullOrEmpty(assetPath))
            return;

        LevelData temporaryLevel = null;

        try
        {
            ValidateSettings(assetPath);

            int[] cells = PixelPatternGenerator.Generate(
                width,
                height,
                colorCount,
                regionCount,
                seed,
                ovalShape,
                minimumColorCount);

            int[] counts = new int[palette.Count];
            int blockCount = 0;

            for (int i = 0; i < cells.Length; i++)
            {
                int color = cells[i];

                if (color == BoardState.EmptyCell)
                    continue;

                counts[color]++;
                blockCount++;
            }

            int actualColorCount = 0;

            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > 0)
                    actualColorCount++;
            }

            // Üretim sonucunu ayrıca kontrol et.
            if (actualColorCount < minimumColorCount ||
                actualColorCount > colorCount)
            {
                throw new InvalidOperationException(
                    "Üretilen desen renk sınırlarını karşılamadı. " +
                    "Bölüm kaydedilmedi.");
            }

            temporaryLevel = CreateInstance<LevelData>();
            WriteBoard(temporaryLevel, cells);

            ShooterData[] shooters = ShooterGenerator.Generate(
                temporaryLevel,
                conveyorCapacity,
                waitingCapacity,
                cellSize,
                distanceFromBoard,
                entryClearance,
                movementSpeed,
                maxShooters,
                timeLimitMilliseconds,
                maxAmmoPerShooter, 
                seed);

            WriteShooters(temporaryLevel, shooters);

            AssetDatabase.CreateAsset(temporaryLevel, assetPath);

            LevelData savedLevel = temporaryLevel;
            temporaryLevel = null;

            AssetDatabase.SaveAssetIfDirty(savedLevel);

            Selection.activeObject = savedLevel;
            EditorGUIUtility.PingObject(savedLevel);

            var text = new StringBuilder();

            text.AppendLine("Bölüm üretildi ve çözümü doğrulandı.");
            text.AppendLine();
            text.AppendLine($"Boyut: {width} × {height}");
            text.AppendLine($"Küp: {blockCount}");
            text.AppendLine($"Kullanılan renk: {actualColorCount}");
            text.AppendLine($"Shooter: {shooters.Length}");
            text.AppendLine($"Seed: {seed}");
            text.AppendLine();

            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > 0)
                    text.AppendLine($"Renk {i}: {counts[i]} küp");
            }

            text.AppendLine();
            text.AppendLine(
                "Doğrulanan çözüm shooter'ları tek tek gönderir.");

            report = text.ToString();
            reportType = MessageType.Info;
        }
        catch (Exception exception)
        {
            report = exception.Message;
            reportType = MessageType.Error;
            Debug.LogException(exception);
        }
        finally
        {
            if (temporaryLevel != null)
                DestroyImmediate(temporaryLevel);
        }
    }

    private void ValidateSettings(string assetPath)
    {
        if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
        {
            throw new InvalidOperationException(
                "Bu isimde bir dosya var. Yeni bir dosya adı seç.");
        }

        if (palette == null || palette.Count < 1)
        {
            throw new InvalidOperationException(
                "En az bir renk içeren palet gerekli.");
        }

        if (colorCount < 1 || colorCount > palette.Count ||
            minimumColorCount < 1 || minimumColorCount > colorCount)
        {
            throw new InvalidOperationException(
                "Minimum ve maksimum renk ayarlarını kontrol et.");
        }

        if (conveyorCapacity < 1 || waitingCapacity < 1)
        {
            throw new InvalidOperationException(
                "Bant ve bekleme kapasitesi en az 1 olmalı.");
        }
    }

    private void WriteBoard(LevelData target, int[] cells)
    {
        var serialized = new SerializedObject(target);

        serialized.FindProperty("palette").objectReferenceValue = palette;
        serialized.FindProperty("width").intValue = width;
        serialized.FindProperty("height").intValue = height;

        serialized.FindProperty("conveyorCapacity").intValue =
            conveyorCapacity;

        serialized.FindProperty("waitingCapacity").intValue =
            waitingCapacity;

        SerializedProperty array = serialized.FindProperty("cells");
        array.arraySize = cells.Length;

        for (int i = 0; i < cells.Length; i++)
            array.GetArrayElementAtIndex(i).intValue = cells[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WriteShooters(
        LevelData target,
        ShooterData[] shooters)
    {
        var serialized = new SerializedObject(target);
        serialized.Update();

        SerializedProperty array =
            serialized.FindProperty("shooters");

        if (array == null)
        {
            throw new InvalidOperationException(
                "LevelData içinde 'shooters' alanı bulunamadı.");
        }

        array.arraySize = shooters.Length;

        for (int i = 0; i < shooters.Length; i++)
        {
            SerializedProperty element =
                array.GetArrayElementAtIndex(i);

            SerializedProperty color =
                element.FindPropertyRelative("colorId");

            SerializedProperty ammo =
                element.FindPropertyRelative("ammo");

            SerializedProperty lane =
                element.FindPropertyRelative("laneIndex");

            if (color == null || ammo == null || lane == null)
            {
                throw new InvalidOperationException(
                    "ShooterData alanları eksik. " +
                    "colorId, ammo ve laneIndex bulunmalı.");
            }

            color.intValue = shooters[i].ColorId;
            ammo.intValue = shooters[i].Ammo;
            lane.intValue = shooters[i].LaneIndex;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}