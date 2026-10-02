using System;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class WaitingPuzzleGeneratorWindow : EditorWindow
{
    [SerializeField] private LevelData level;

    [SerializeField] private int maxCombinedAmmo = 60;
    [SerializeField] private int maxCandidates = 8;
    [SerializeField] private int timeLimitMilliseconds = 8000;

    private Vector2 scroll;
    private string report;
    private MessageType reportType;

    [MenuItem("Tools/ColorLoop/Waiting Puzzle Generator")]
    private static void Open()
    {
        var window = GetWindow<WaitingPuzzleGeneratorWindow>(
            "Waiting Puzzle Generator");

        window.minSize = new Vector2(450f, 450f);
        window.Show();
    }

    private void OnEnable()
    {
        if (level == null && Selection.activeObject is LevelData selected)
            level = selected;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField(
            "Beklemeye Dönüş İçeren Çözüm Üret",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Mevcut listedeki aynı renk shooter'ları birleştirerek " +
            "adaylar dener.\n\n" +
            "Kazanan çözümde bekleyen bir shooter yeniden " +
            "gönderilmişse liste kaydedilir. " +
            "Başarısız aramada bölüm değiştirilmez.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        level = (LevelData)EditorGUILayout.ObjectField(
            "Bölüm",
            level,
            typeof(LevelData),
            false);

        maxCombinedAmmo = EditorGUILayout.IntField(
            "Birleşik Mermi Üst Sınırı",
            maxCombinedAmmo);

        maxCandidates = EditorGUILayout.IntField(
            "En Fazla Aday",
            maxCandidates);

        timeLimitMilliseconds = EditorGUILayout.IntField(
            "Toplam Süre Sınırı (ms)",
            timeLimitMilliseconds);

        if (EditorGUI.EndChangeCheck())
            report = null;

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Arama ana iş parçacığında çalışır. İşlem sırasında " +
            "Unity kısa süre yanıt vermeyebilir.\n\n" +
            "Bu kontrol, beklemeye dönüşün zorunlu olduğunu " +
            "veya bölümün zor olduğunu kanıtlamaz.",
            MessageType.Info);

        bool playMode =
            EditorApplication.isPlayingOrWillChangePlaymode;

        using (new EditorGUI.DisabledScope(level == null || playMode))
        {
            if (GUILayout.Button(
                    "Aday Ara, Doğrula ve Kaydet",
                    GUILayout.Height(36f)))
            {
                Generate();
            }
        }

        if (!string.IsNullOrEmpty(report))
        {
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Son aramanın raporu",
                reportType);

            EditorGUILayout.TextArea(
                report,
                GUILayout.MinHeight(200f));
        }

        EditorGUILayout.EndScrollView();
    }

    private void Generate()
    {
        report = null;

        try
        {
            WaitingPuzzleResult result = WaitingPuzzleGenerator.Generate(
                level,
                maxCombinedAmmo,
                maxCandidates,
                timeLimitMilliseconds);

            if (result == null)
            {
                reportType = MessageType.Warning;

                report =
                    "Bu adaylar ve arama sınırları içinde, beklemeye " +
                    "dönüş içeren doğrulanmış çözüm bulunamadı.\n\n" +
                    "Mevcut bölüm değiştirilmedi.\n" +
                    "Bu sonuç böyle bir çözümün imkânsız olduğunu göstermez.";

                return;
            }

            SaveShooters(result.Shooters);

            var text = new StringBuilder();

            text.AppendLine($"Bölüm: {level.name}");
            text.AppendLine("SONUÇ: Beklemeye dönüş içeren çözüm doğrulandı.");
            text.AppendLine($"Denenen aday: {result.AttemptCount}");
            text.AppendLine($"Shooter sayısı: {result.Shooters.Length}");
            text.AppendLine("Resim değiştirilmedi.");
            text.AppendLine();

            text.AppendLine("Shooter listesi:");

            for (int i = 0; i < result.Shooters.Length; i++)
            {
                ShooterData shooter = result.Shooters[i];

                text.AppendLine(
                    $"{i}: Sıra {shooter.LaneIndex}, " +
                    $"renk {shooter.ColorId}, {shooter.Ammo} mermi");
            }

            text.AppendLine();
            text.AppendLine("Doğrulanan çözüm:");

            for (int i = 0; i < result.Solution.Length; i++)
            {
                SolverAction action = result.Solution[i];

                if (action.Type == SolverActionType.Wait)
                {
                    text.AppendLine(
                        $"{i + 1}. {action.Seconds:0.###} saniye bekle.");
                }
                else
                {
                    text.AppendLine(
                        $"{i + 1}. Shooter {action.ShooterIndex} gönder.");
                }
            }

            text.AppendLine();
            text.AppendLine(
                "Aynı shooter numarası tekrar geçtiğinde " +
                "bekleme alanındaki shooter yeniden gönderilir.");

            text.AppendLine(
                "Liste kaydedildi. Ctrl+Z ile geri alınabilir.");

            report = text.ToString();
            reportType = MessageType.Info;
        }
        catch (Exception exception)
        {
            reportType = MessageType.Error;
            report = exception.Message;
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
                "LevelData içinde shooters alanı bulunamadı.");
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
                    "ShooterData alanları eksik.");
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