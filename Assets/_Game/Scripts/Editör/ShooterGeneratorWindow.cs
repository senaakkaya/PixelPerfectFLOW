using System;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class ShooterGeneratorWindow : EditorWindow
{
    [SerializeField] private LevelData level;

    [SerializeField] private float cellSize = 0.15f;
    [SerializeField] private float distanceFromBoard = 0.7f;
    [SerializeField] private float entryClearance = 0.6f;
    [SerializeField] private float movementSpeed = 1.5f;

    [SerializeField] private int maxAmmoPerShooter = 30;
    [SerializeField] private int maxShooters = 512;
    [SerializeField] private int timeLimitMilliseconds = 3000;
    [SerializeField] private int distributionSeed = 12345;

    private Vector2 scroll;
    private string report;
    private MessageType reportType;

    [MenuItem("Tools/ColorLoop/Shooter Generator")]
    private static void Open()
    {
        var window =
            GetWindow<ShooterGeneratorWindow>("Shooter Generator");

        window.minSize = new Vector2(430f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        if (level == null && Selection.activeObject is LevelData selected)
            level = selected;

        Undo.undoRedoPerformed += HandleUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= HandleUndoRedo;
    }

    private void HandleUndoRedo()
    {
        report = null;
        Repaint();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField(
            "Resme Göre Shooter Üret",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Resim korunur. Shooter listesi, simülasyonda doğrulanan " +
            "yeni listeyle değiştirilir.\n\n" +
            "Doğrulanan çözüm shooter'ları sırayla gönderir; " +
            "eşzamanlı gönderme zorunluluğu üretmez.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        level = (LevelData)EditorGUILayout.ObjectField(
            "Bölüm", level, typeof(LevelData), false);

        EditorGUILayout.Space();

        DrawLevelRules();

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
            "Üretim Ayarları",
            EditorStyles.boldLabel);

        maxAmmoPerShooter = EditorGUILayout.IntField(
            "Shooter Başına En Fazla Mermi",
            maxAmmoPerShooter);
        
        distributionSeed = EditorGUILayout.IntField(
            "Sıra Dağıtım Seed",
            distributionSeed);

        maxShooters = EditorGUILayout.IntField(
            "En Fazla Shooter", maxShooters);

        timeLimitMilliseconds = EditorGUILayout.IntField(
            "Süre Sınırı (ms)", timeLimitMilliseconds);

        if (EditorGUI.EndChangeCheck())
            report = null;

        EditorGUILayout.Space();

        bool playMode =
            EditorApplication.isPlayingOrWillChangePlaymode;

        if (playMode)
        {
            EditorGUILayout.HelpBox(
                "Üretim için Play modundan çık.",
                MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(level == null || playMode))
        {
            if (GUILayout.Button(
                    "Üret, Doğrula ve Shooter Listesini Kaydet",
                    GUILayout.Height(36f)))
            {
                Generate();
            }
        }

        if (!string.IsNullOrEmpty(report))
        {
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Son üretim raporu",
                reportType);

            EditorGUILayout.TextArea(
                report,
                GUILayout.MinHeight(200f));
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawLevelRules()
    {
        EditorGUILayout.LabelField(
            "Bölümden Okunan Kapasiteler",
            EditorStyles.boldLabel);

        if (level == null)
        {
            EditorGUILayout.HelpBox(
                "Bir LevelData dosyası seç.",
                MessageType.Info);

            return;
        }

        EditorGUILayout.LabelField(
            "Bant Kapasitesi",
            level.ConveyorCapacity.ToString());

        EditorGUILayout.LabelField(
            "Bekleme Kapasitesi",
            level.WaitingCapacity.ToString());
    }

    private void Generate()
    {
        report = null;

        try
        {
            int conveyorCapacity = level.ConveyorCapacity;
            int waitingCapacity = level.WaitingCapacity;

            ShooterData[] generated = ShooterGenerator.Generate(
                level,
                conveyorCapacity,
                waitingCapacity,
                cellSize,
                distanceFromBoard,
                entryClearance,
                movementSpeed,
                maxShooters,
                timeLimitMilliseconds,
                maxAmmoPerShooter,
                distributionSeed);

            SaveShooters(generated);

            long totalAmmo = 0;
            var text = new StringBuilder();
            text.AppendLine($"Bölüm: {level.name}");
            text.AppendLine("SONUÇ: Liste üretildi ve doğrulandı.");
            text.AppendLine("Resim değiştirilmedi.");
            text.AppendLine($"Bant kapasitesi: {conveyorCapacity}");
            text.AppendLine($"Bekleme kapasitesi: {waitingCapacity}");
            text.AppendLine($"Mermi üst sınırı: {maxAmmoPerShooter}");
            text.AppendLine($"Shooter sayısı: {generated.Length}");
            text.AppendLine();
            text.AppendLine("Sıralar: 0 = Sol, 1 = Orta, 2 = Sağ");
            text.AppendLine();

            for (int i = 0; i < generated.Length; i++)
            {
                ShooterData shooter = generated[i];
                totalAmmo += shooter.Ammo;

                text.AppendLine(
                    $"{i}. Sıra {shooter.LaneIndex} — " +
                    $"Renk {shooter.ColorId} — " +
                    $"{shooter.Ammo} mermi");
            }

            text.AppendLine();
            text.AppendLine($"Toplam mühimmat: {totalAmmo}");
            text.AppendLine();

            text.AppendLine(
                "Doğrulanan çözüm: Yukarıdaki listeyi baştan sona takip et. " +
                "Her adımda belirtilen sıranın başındaki shooter'ı gönder, " +
                "bir tur süresi bekle ve sonraki adıma geç.");

            text.AppendLine(
                "Liste kaydedildi. Ctrl+Z ile geri alınabilir.");

            report = text.ToString();
            reportType = MessageType.Info;
        }
        catch (Exception exception)
        {
            report = exception.Message;
            reportType = MessageType.Error;
            Debug.LogException(exception);
        }
    }

    private void SaveShooters(ShooterData[] shooters)
    {
        var serialized = new SerializedObject(level);
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

        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssetIfDirty(level);
    }
}