using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "GameTuning",
    menuName = "ColorLoop/Game Tuning")]
public sealed class GameTuning : ScriptableObject
{
    [Header("Tahta")]
    [SerializeField, Min(0.01f)]
    private float cellSize = 0.15f;

    [Header("Bant")]
    [SerializeField, Min(0.01f)]
    private float distanceFromBoard = 0.7f;

    [SerializeField, Min(0.01f)]
    private float entryClearance = 0.6f;

    [Header("Shooter")]
    [SerializeField, Min(0.01f)]
    private float movementSpeed = 1.5f;

    public float CellSize => cellSize;
    public float DistanceFromBoard => distanceFromBoard;
    public float EntryClearance => entryClearance;
    public float MovementSpeed => movementSpeed;

    private static GameTuning cached;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cached = null;
    }

    public static GameTuning Load()
    {
        if (cached == null)
            cached = Resources.Load<GameTuning>("GameTuning");

        if (cached == null)
        {
            throw new InvalidOperationException(
                "GameTuning bulunamadı. Resources klasöründe " +
                "GameTuning isimli ayar dosyası oluştur.");
        }

        cached.ValidateValues();
        return cached;
    }

    private void ValidateValues()
    {
        ValidatePositive(cellSize, "Hücre Boyutu");
        ValidatePositive(distanceFromBoard, "Tahtadan Yol Uzaklığı");
        ValidatePositive(entryClearance, "Giriş Mesafesi");
        ValidatePositive(movementSpeed, "Shooter Hızı");
    }

    private static void ValidatePositive(float value, string label)
    {
        if (float.IsNaN(value) ||
            float.IsInfinity(value) ||
            value <= 0f)
        {
            throw new InvalidOperationException(
                $"GameTuning: {label} sıfırdan büyük olmalı.");
        }
    }
}