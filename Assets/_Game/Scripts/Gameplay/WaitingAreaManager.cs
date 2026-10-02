using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class WaitingAreaManager : MonoBehaviour
{
    [Header("Bölüm")]
    [SerializeField] private LevelData level;

    [Header("Bağlantılar")]
    [SerializeField] private ConveyorManager conveyor;

    [SerializeField] private Transform[] slots;

    public event Action WaitingAreaFull;

    private ConveyorMover[] occupants;

    private readonly HashSet<ConveyorMover> registeredShooters =
        new HashSet<ConveyorMover>();

    public bool IsReady => occupants != null;
    public int Capacity => occupants != null ? occupants.Length : 0;

    private void Awake()
    {
        if (LevelSession.CurrentLevel != null)
            level = LevelSession.CurrentLevel;
        
        if (level == null || conveyor == null)
        {
            Fail("Level ve Conveyor atanmalı.");
            return;
        }

        int capacity = level.WaitingCapacity;

        if (slots == null || slots.Length < capacity)
        {
            Fail(
                $"Bölüm {capacity} bekleme slotu istiyor. " +
                "Slots listesinde yeterli Transform yok.");

            return;
        }

        for (int i = 0; i < capacity; i++)
        {
            if (slots[i] == null)
            {
                Fail($"Slots listesindeki {i}. eleman boş.");
                return;
            }

            for (int j = 0; j < i; j++)
            {
                if (slots[i] != slots[j])
                    continue;

                Fail(
                    $"Slots listesindeki {i} ve {j} " +
                    "aynı Transform'u kullanıyor.");

                return;
            }
        }

        occupants = new ConveyorMover[capacity];
    }

    public void RegisterShooter(ConveyorMover mover)
    {
        if (!IsReady || mover == null)
            return;

        if (!registeredShooters.Add(mover))
            return;

        mover.LapCompleted += HandleLapCompleted;
    }

    private void HandleLapCompleted(ConveyorMover mover)
    {
        if (!IsReady || !isActiveAndEnabled || mover == null)
            return;

        if (!mover.TryGetComponent(
                out ShooterController shooter))
        {
            Debug.LogError(
                "Bekleme alanına dönen nesnede ShooterController yok.",
                mover);

            return;
        }

        if (shooter.RemainingAmmo <= 0)
        {
            mover.gameObject.SetActive(false);
            return;
        }

        int emptySlot = -1;

        for (int i = 0; i < occupants.Length; i++)
        {
            // Aynı shooter iki slota kaydedilmesin.
            if (occupants[i] == mover)
                return;

            if (emptySlot < 0 && occupants[i] == null)
                emptySlot = i;
        }

        if (emptySlot < 0)
        {
            WaitingAreaFull?.Invoke();
            return;
        }

        occupants[emptySlot] = mover;

        mover.transform.SetPositionAndRotation(
            slots[emptySlot].position,
            slots[emptySlot].rotation);
    }

    public bool TrySendToConveyor(int slotIndex)
    {
        if (!IsReady || !isActiveAndEnabled)
            return false;

        if (slotIndex < 0 || slotIndex >= occupants.Length)
            return false;

        ConveyorMover mover = occupants[slotIndex];

        if (mover == null)
            return false;

        // Gönderme başarısızsa shooter slotunda kalır.
        if (!conveyor.TryEnter(mover))
            return false;

        occupants[slotIndex] = null;
        return true;
    }

    public bool TrySendShooter(ConveyorMover mover)
    {
        if (!IsReady || !isActiveAndEnabled || mover == null)
            return false;

        for (int i = 0; i < occupants.Length; i++)
        {
            if (occupants[i] == mover)
                return TrySendToConveyor(i);
        }

        return false;
    }

    [ContextMenu("Test: İlk Bekleyen Shooter'ı Gönder")]
    private void TestSendFirstWaitingShooter()
    {
        if (!Application.isPlaying || !IsReady)
            return;

        for (int i = 0; i < occupants.Length; i++)
        {
            if (occupants[i] == null)
                continue;

            TrySendToConveyor(i);
            return;
        }
    }

    private void Fail(string message)
    {
        Debug.LogError("WaitingAreaManager: " + message, this);
        enabled = false;
    }

    private void OnDestroy()
    {
        foreach (ConveyorMover mover in registeredShooters)
        {
            if (mover != null)
                mover.LapCompleted -= HandleLapCompleted;
        }

        registeredShooters.Clear();
    }
}