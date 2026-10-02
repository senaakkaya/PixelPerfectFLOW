using System;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class LevelSolverWindow : EditorWindow
{
    [SerializeField] private LevelData level;

    [SerializeField] private float cellSize = 0.15f;
    [SerializeField] private float distanceFromBoard = 0.7f;
    [SerializeField] private float entryClearance = 0.6f;
    [SerializeField] private float movementSpeed = 1.5f;

    [SerializeField] private int maxVisitedStates = 5000;
    [SerializeField] private int maxDepth = 256;
    [SerializeField] private int timeLimitMilliseconds = 1000;
    [SerializeField] private float waitSeconds = 0.25f;

    private Vector2 scroll;
    private string report;
    private MessageType reportType;

    [MenuItem("Tools/ColorLoop/Level Solver")]
    private static void Open()
    {
        var window = GetWindow<LevelSolverWindow>("Level Solver");
        window.minSize = new Vector2(420f, 540f);
        window.Show();
    }

    private void OnEnable()
    {
        if (level == null && Selection.activeObject is LevelData selected)
            level = selected;

        EditorApplication.projectChanged += HandleProjectChanged;
        Undo.undoRedoPerformed += HandleProjectChanged;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= HandleProjectChanged;
        Undo.undoRedoPerformed -= HandleProjectChanged;
    }

    private void HandleProjectChanged()
    {
        report = null;
        Repaint();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField(
            "Bölüm Çözüm Kontrolü",
            EditorStyles.boldLabel);

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
            "Arama Ayarları",
            EditorStyles.boldLabel);

        maxVisitedStates = EditorGUILayout.IntField(
            "En Fazla Durum", maxVisitedStates);

        maxDepth = EditorGUILayout.IntSlider(
            "En Fazla Hamle", maxDepth, 1, 512);

        timeLimitMilliseconds = EditorGUILayout.IntField(
            "Süre Sınırı (ms)", timeLimitMilliseconds);

        waitSeconds = EditorGUILayout.FloatField(
            "Bekleme Adımı (sn)", waitSeconds);

        if (EditorGUI.EndChangeCheck())
            report = null;

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Çözüm bulunamaması kesin çözümsüzlük anlamına gelmez. " +
            "Arama sınırları ve bekleme adımı sonucu etkiler.",
            MessageType.Info);

        bool playMode =
            EditorApplication.isPlayingOrWillChangePlaymode;

        if (playMode)
        {
            EditorGUILayout.HelpBox(
                "Kontrol için Play modundan çık.",
                MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(level == null || playMode))
        {
            if (GUILayout.Button(
                    "Bölümde Çözüm Ara",
                    GUILayout.Height(32f)))
            {
                SolveSelectedLevel();
            }
        }

        if (!string.IsNullOrEmpty(report))
        {
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Son çalıştırmanın raporu",
                reportType);

            EditorGUILayout.TextArea(
                report,
                GUILayout.MinHeight(220f));
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

        EditorGUILayout.HelpBox(
            "Bu değerleri değiştirmek için bölüm dosyasını " +
            "Inspector'da düzenle.",
            MessageType.Info);
    }

    private void SolveSelectedLevel()
    {
        report = null;

        try
        {
            int conveyorCapacity = level.ConveyorCapacity;
            int waitingCapacity = level.WaitingCapacity;

            GameSimulator source = LevelSimulationFactory.Create(
                level,
                conveyorCapacity,
                waitingCapacity,
                cellSize,
                distanceFromBoard,
                entryClearance,
                movementSpeed);

            var settings = new SolverSettings
            {
                MaxVisitedStates = maxVisitedStates,
                MaxDepth = maxDepth,
                TimeLimitMilliseconds = timeLimitMilliseconds,
                WaitSeconds = waitSeconds
            };

            SolverResult result = LevelSolver.Solve(source, settings);

            if (result.Status == SolverStatus.Solved)
                VerifySolution(source, result.Actions);

            report = BuildReport(
                result,
                conveyorCapacity,
                waitingCapacity);

            reportType = result.Status == SolverStatus.Solved
                ? MessageType.Info
                : MessageType.Warning;
        }
        catch (Exception exception)
        {
            reportType = MessageType.Error;
            report = "Kontrol tamamlanamadı:\n\n" + exception.Message;
            Debug.LogException(exception);
        }

        Repaint();
    }

    private static void VerifySolution(
        GameSimulator source,
        SolverAction[] actions)
    {
        GameSimulator replay = source.Clone();

        for (int i = 0; i < actions.Length; i++)
        {
            SolverAction action = actions[i];

            switch (action.Type)
            {
                case SolverActionType.Launch:
                {
                    LaunchResult result =
                        replay.TryLaunch(action.ShooterIndex);

                    if (result != LaunchResult.Success)
                    {
                        throw new InvalidOperationException(
                            $"Çözüm doğrulanamadı. Hamle {i + 1}: {result}.");
                    }

                    break;
                }

                case SolverActionType.Wait:
                    replay.Advance(action.Seconds);
                    break;

                default:
                    throw new InvalidOperationException(
                        "Bilinmeyen hamle türü.");
            }
        }

        if (replay.State.Status != SimulationStatus.Won)
        {
            throw new InvalidOperationException(
                "Çözüm tekrar oynatıldığında kazanma oluşmadı.");
        }
    }

    private string BuildReport(
        SolverResult result,
        int conveyorCapacity,
        int waitingCapacity)
    {
        var text = new StringBuilder();

        text.AppendLine($"Bölüm: {level.name}");
        text.AppendLine($"Boyut: {level.Width} × {level.Height}");
        text.AppendLine($"Shooter sayısı: {level.ShooterCount}");
        text.AppendLine($"Bant kapasitesi: {conveyorCapacity}");
        text.AppendLine($"Bekleme kapasitesi: {waitingCapacity}");
        text.AppendLine();

        switch (result.Status)
        {
            case SolverStatus.Solved:
                text.AppendLine("SONUÇ: Çözüm bulundu ve tekrar doğrulandı.");
                break;

            case SolverStatus.InsufficientAmmo:
                text.AppendLine("SONUÇ: Mühimmat yetersiz.");
                break;

            default:
                text.AppendLine(
                    "SONUÇ: Arama sınırları içinde çözüm bulunamadı.");
                break;
        }

        text.AppendLine(result.Message);
        text.AppendLine();

        text.AppendLine($"Ziyaret edilen durum: {result.VisitedStates}");
        text.AppendLine($"Arama süresi: {result.ElapsedMilliseconds} ms");
        text.AppendLine($"Hamle sayısı: {result.Actions.Length}");

        if (result.Status != SolverStatus.Solved)
            return text.ToString();

        text.AppendLine();
        text.AppendLine("Çözüm — shooter numaraları 0'dan başlar:");

        for (int i = 0; i < result.Actions.Length; i++)
        {
            SolverAction action = result.Actions[i];

            if (action.Type == SolverActionType.Launch)
            {
                text.AppendLine(
                    $"{i + 1}. Shooter {action.ShooterIndex} gönder.");
            }
            else
            {
                text.AppendLine(
                    $"{i + 1}. {action.Seconds:0.###} saniye bekle.");
            }
        }

        return text.ToString();
    }
}