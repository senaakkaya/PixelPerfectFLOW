using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ConveyorManager : MonoBehaviour
{
    [Header("Bölüm")]
    [SerializeField] private LevelData level;

    [Header("Bant")]
    [SerializeField] private ConveyorPath path;

    [SerializeField, Min(0.01f)]
    private float entryClearance = 0.6f;

    private readonly List<ConveyorMover> activeShooters =
        new List<ConveyorMover>();

    private int capacity;
    private bool isReady;

    public int Capacity => capacity;
    public int ActiveCount => activeShooters.Count;
    public bool IsReady => isReady;

    private void Awake()
    {
        entryClearance = GameTuning.Load().EntryClearance;
        if (LevelSession.CurrentLevel != null)
            level = LevelSession.CurrentLevel;
        
        if (level == null || path == null)
        {
            Debug.LogError(
                "ConveyorManager: Level ve Path atanmalı.",
                this);

            enabled = false;
            return;
        }

        if (float.IsNaN(entryClearance) ||
            float.IsInfinity(entryClearance) ||
            entryClearance <= 0f)
        {
            Debug.LogError(
                "ConveyorManager: Entry Clearance sıfırdan büyük olmalı.",
                this);

            enabled = false;
            return;
        }

        capacity = level.ConveyorCapacity;
        activeShooters.Capacity = capacity;

        isReady = true;
    }

    private void Update()
    {
        if (!isReady)
            return;

        RemoveInactiveShooters();

        double remainingTime = Time.deltaTime;

        while (activeShooters.Count > 0)
        {
            int nextIndex = FindNextEvent(out double eventTime);

            if (nextIndex < 0)
                return;

            // Bu karede hiçbir atış veya tur sonu olmayacak.
            if (eventTime > remainingTime)
            {
                AdvanceAll(remainingTime);
                return;
            }

            ConveyorMover nextShooter = activeShooters[nextIndex];

            // Bütün shooter'ları olayın zamanına getir.
            AdvanceAll(eventTime);

            remainingTime = Math.Max(
                0d,
                remainingTime - eventTime);

            // Bu çağrı atış yapabilir, turu bitirebilir
            // veya oyunun bitmesine neden olabilir.
            nextShooter.ProcessNextEvent();

            RemoveInactiveShooters();

            // Süre sıfır olsa da aynı zamandaki diğer olaylar işlenir.
        }
    }

    public bool TryEnter(ConveyorMover mover)
    {
        if (!isReady || !isActiveAndEnabled)
            return false;

        if (mover == null || !mover.isActiveAndEnabled)
            return false;

        RemoveInactiveShooters();

        if (mover.IsMoving || activeShooters.Contains(mover))
            return false;

        if (activeShooters.Count >= capacity)
            return false;

        if (!mover.TryGetComponent(
                out ShooterController shooter))
        {
            return false;
        }

        if (!shooter.IsReady || shooter.RemainingAmmo <= 0)
            return false;

        if (!path.TryGetPose(
                0f,
                out Vector3 entryPosition,
                out _))
        {
            return false;
        }

        float clearanceSquared =
            entryClearance * entryClearance;

        for (int i = 0; i < activeShooters.Count; i++)
        {
            ConveyorMover active = activeShooters[i];

            float distanceSquared =
                (active.transform.position - entryPosition).sqrMagnitude;

            if (distanceSquared < clearanceSquared)
                return false;
        }

        mover.BeginLap();

        if (!mover.IsMoving)
            return false;

        // Listenin sonuna eklemek giriş sırasını korur.
        activeShooters.Add(mover);

        mover.LapCompleted += HandleLapCompleted;

        return true;
    }

    private int FindNextEvent(out double eventTime)
    {
        int selected = -1;
        eventTime = double.PositiveInfinity;

        for (int i = 0; i < activeShooters.Count; i++)
        {
            double candidateTime =
                activeShooters[i].GetTimeUntilNextEvent();

            // Eşit zamanda ilk bulunan, yani önce giren kazanır.
            if (candidateTime >= eventTime)
                continue;

            selected = i;
            eventTime = candidateTime;
        }

        return selected;
    }

    private void AdvanceAll(double seconds)
    {
        for (int i = 0; i < activeShooters.Count; i++)
            activeShooters[i].AdvanceMovement(seconds);
    }

    private void HandleLapCompleted(ConveyorMover mover)
    {
        mover.LapCompleted -= HandleLapCompleted;
        activeShooters.Remove(mover);
    }

    private void RemoveInactiveShooters()
    {
        for (int i = activeShooters.Count - 1; i >= 0; i--)
        {
            ConveyorMover mover = activeShooters[i];

            if (mover != null &&
                mover.isActiveAndEnabled &&
                mover.IsMoving)
            {
                continue;
            }

            if (mover != null)
                mover.LapCompleted -= HandleLapCompleted;

            activeShooters.RemoveAt(i);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < activeShooters.Count; i++)
        {
            ConveyorMover mover = activeShooters[i];

            if (mover != null)
                mover.LapCompleted -= HandleLapCompleted;
        }

        activeShooters.Clear();
    }
}