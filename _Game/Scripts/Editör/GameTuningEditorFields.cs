using UnityEditor;
using UnityEngine;

public static class GameTuningEditorFields
{
    public static void Apply(
        ref float cellSize,
        ref float distanceFromBoard,
        ref float entryClearance,
        ref float movementSpeed)
    {
        GameTuning tuning = GameTuning.Load();

        cellSize = tuning.CellSize;
        distanceFromBoard = tuning.DistanceFromBoard;
        entryClearance = tuning.EntryClearance;
        movementSpeed = tuning.MovementSpeed;
    }

    public static void Draw(
        ref float cellSize,
        ref float distanceFromBoard,
        ref float entryClearance,
        ref float movementSpeed)
    {
        Apply(
            ref cellSize,
            ref distanceFromBoard,
            ref entryClearance,
            ref movementSpeed);

        EditorGUILayout.LabelField(
            "Ortak Oynanış Ayarları",
            EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField("Hücre Boyutu", cellSize);

            EditorGUILayout.FloatField(
                "Tahtadan Yol Uzaklığı", distanceFromBoard);

            EditorGUILayout.FloatField(
                "Giriş Mesafesi", entryClearance);

            EditorGUILayout.FloatField(
                "Shooter Hızı", movementSpeed);
        }

        if (GUILayout.Button("Ortak Ayar Dosyasını Seç"))
        {
            GameTuning tuning = GameTuning.Load();
            Selection.activeObject = tuning;
            EditorGUIUtility.PingObject(tuning);
        }
    }
}