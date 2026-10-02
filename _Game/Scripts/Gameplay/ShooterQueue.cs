using System.Collections.Generic;
using UnityEngine;

public sealed class ShooterQueue : MonoBehaviour
{
    [Header("Bağlantılar")]
    [SerializeField] private ConveyorManager conveyor;

    [Tooltip("LevelSession yoksa kullanılacak bölüm.")]
    [SerializeField] private LevelData level;

    [SerializeField] private ConveyorMover[] shooters;

    [Header("Üç Sıralı Yerleşim")]
    [Tooltip("Sol, orta ve sağ sıra arasındaki mesafe.")]
    [SerializeField]
    private Vector3 spacing = new Vector3(0.6f, 0f, 0f);

    [Tooltip("Aynı sırada arkaya doğru ilerleme yönü.")]
    [SerializeField]
    private Vector3 rowSpacing = new Vector3(0f, 0f, -0.6f);

    [SerializeField, Min(1)]
    private int visiblePerLane = 3;

    [SerializeField, Min(0f)]
    private float shiftDuration = 0.15f;

    private QueueEntry[] entries;
    private int[] laneHeads;
    private int remainingCount;
    private bool initialized;

    private readonly Dictionary<ConveyorMover, int> indexByMover =
        new Dictionary<ConveyorMover, int>();

    public int RemainingCount => remainingCount;

    private sealed class QueueEntry
    {
        public ConveyorMover Mover;

        public int Lane;
        public int NextInLane = -1;

        public Renderer[] Renderers;
        public bool[] RendererDefaults;

        public Collider[] Colliders;
        public bool[] ColliderDefaults;

        public ShooterAmmoLabel AmmoLabel;
        public bool LabelDefault;

        public bool Visible = true;
        public bool Sent;

        public Vector3 MoveStart;
        public Vector3 MoveTarget;
        public float MoveElapsed;
        public bool Moving;
    }

    private void Start()
    {
        if (!initialized)
            SetShooters(shooters);
    }

    public void SetShooters(ConveyorMover[] newShooters)
    {
        LevelData source = LevelSession.CurrentLevel != null
            ? LevelSession.CurrentLevel
            : level;

        if (conveyor == null || source == null)
        {
            Debug.LogError(
                "ShooterQueue: Conveyor veya bölüm bağlantısı eksik.",
                this);

            return;
        }

        if (newShooters == null ||
            newShooters.Length != source.ShooterCount)
        {
            Debug.LogError(
                "ShooterQueue: Oluşturulan shooter sayısı " +
                "bölüm listesindeki sayıyla eşleşmiyor.",
                this);

            return;
        }

        var unique = new HashSet<ConveyorMover>();

        for (int i = 0; i < newShooters.Length; i++)
        {
            if (newShooters[i] == null ||
                !unique.Add(newShooters[i]))
            {
                Debug.LogError(
                    $"ShooterQueue: {i}. shooter boş veya tekrar ediyor.",
                    this);

                return;
            }

            int lane = source.GetShooter(i).LaneIndex;

            if (lane < 0 || lane >= LevelData.QueueLaneCount)
            {
                Debug.LogError(
                    $"ShooterQueue: {i}. shooter için " +
                    "Lane Index 0, 1 veya 2 olmalı.",
                    this);

                return;
            }
        }

        RestoreOldQueue();

        shooters = newShooters;
        entries = new QueueEntry[shooters.Length];

        laneHeads = new int[LevelData.QueueLaneCount];

        for (int lane = 0; lane < laneHeads.Length; lane++)
            laneHeads[lane] = -1;

        indexByMover.Clear();

        for (int i = 0; i < shooters.Length; i++)
        {
            entries[i] = CreateEntry(
                shooters[i],
                source.GetShooter(i).LaneIndex);

            indexByMover.Add(shooters[i], i);
        }

        // Geriye doğru bağlayarak her sıranın kendi zincirini kur.
        for (int i = entries.Length - 1; i >= 0; i--)
        {
            QueueEntry entry = entries[i];

            entry.NextInLane = laneHeads[entry.Lane];
            laneHeads[entry.Lane] = i;
        }

        remainingCount = entries.Length;
        initialized = true;

        for (int lane = 0; lane < laneHeads.Length; lane++)
            RefreshLane(lane, false);
    }

    public bool TrySendShooter(ConveyorMover mover)
    {
        if (!initialized || !isActiveAndEnabled || mover == null)
            return false;

        if (!indexByMover.TryGetValue(mover, out int index))
            return false;

        QueueEntry entry = entries[index];

        if (entry.Sent)
            return false;

        // Her sıranın yalnızca başındaki shooter seçilebilir.
        if (laneHeads[entry.Lane] != index)
            return false;

        if (!conveyor.TryEnter(mover))
            return false;

        entry.Sent = true;
        entry.Moving = false;

        SetVisible(entry, true);

        laneHeads[entry.Lane] = entry.NextInLane;
        remainingCount--;

        // Diğer iki sıra yerinde kalır.
        RefreshLane(entry.Lane, true);

        return true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        int visibleLimit = Mathf.Max(1, visiblePerLane);

        for (int lane = 0; lane < laneHeads.Length; lane++)
        {
            int index = laneHeads[lane];
            int row = 0;

            while (index >= 0 && row < visibleLimit)
            {
                QueueEntry entry = entries[index];
                UpdateMovement(entry);

                index = entry.NextInLane;
                row++;
            }
        }
    }

    private void UpdateMovement(QueueEntry entry)
    {
        if (!entry.Moving || entry.Mover == null)
            return;

        entry.MoveElapsed += Time.deltaTime;

        float t = shiftDuration <= 0f
            ? 1f
            : Mathf.Clamp01(entry.MoveElapsed / shiftDuration);

        float eased = t * t * (3f - 2f * t);

        entry.Mover.transform.position = Vector3.Lerp(
            entry.MoveStart,
            entry.MoveTarget,
            eased);

        if (t >= 1f)
            entry.Moving = false;
    }

    private void RefreshLane(int lane, bool animate)
    {
        int index = laneHeads[lane];
        int row = 0;
        int visibleLimit = Mathf.Max(1, visiblePerLane);

        while (index >= 0)
        {
            QueueEntry entry = entries[index];

            bool shouldShow = row < visibleLimit;
            bool wasVisible = entry.Visible;

            SetVisible(entry, shouldShow);

            if (entry.Mover != null && shouldShow)
            {
                Vector3 localPosition =
                    spacing * lane + rowSpacing * row;

                Vector3 target =
                    transform.TransformPoint(localPosition);

                entry.MoveStart = entry.Mover.transform.position;
                entry.MoveTarget = target;
                entry.MoveElapsed = 0f;

                entry.Moving =
                    animate &&
                    wasVisible &&
                    shiftDuration > 0f;

                if (!entry.Moving)
                    entry.Mover.transform.position = target;
            }
            else
            {
                entry.Moving = false;
            }

            index = entry.NextInLane;
            row++;
        }
    }

    private static QueueEntry CreateEntry(
        ConveyorMover mover,
        int lane)
    {
        var entry = new QueueEntry
        {
            Mover = mover,
            Lane = lane,
            Renderers = mover.GetComponentsInChildren<Renderer>(true),
            Colliders = mover.GetComponentsInChildren<Collider>(true),
            AmmoLabel = mover.GetComponent<ShooterAmmoLabel>()
        };

        entry.RendererDefaults = new bool[entry.Renderers.Length];

        for (int i = 0; i < entry.Renderers.Length; i++)
            entry.RendererDefaults[i] = entry.Renderers[i].enabled;

        entry.ColliderDefaults = new bool[entry.Colliders.Length];

        for (int i = 0; i < entry.Colliders.Length; i++)
            entry.ColliderDefaults[i] = entry.Colliders[i].enabled;

        entry.LabelDefault =
            entry.AmmoLabel != null && entry.AmmoLabel.enabled;

        return entry;
    }

    private static void SetVisible(QueueEntry entry, bool visible)
    {
        if (entry.Visible == visible)
            return;

        entry.Visible = visible;

        for (int i = 0; i < entry.Renderers.Length; i++)
        {
            if (entry.Renderers[i] != null)
            {
                entry.Renderers[i].enabled =
                    visible && entry.RendererDefaults[i];
            }
        }

        for (int i = 0; i < entry.Colliders.Length; i++)
        {
            if (entry.Colliders[i] != null)
            {
                entry.Colliders[i].enabled =
                    visible && entry.ColliderDefaults[i];
            }
        }

        if (entry.AmmoLabel != null)
            entry.AmmoLabel.enabled = visible && entry.LabelDefault;
    }

    private void RestoreOldQueue()
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            QueueEntry entry = entries[i];

            if (entry == null || entry.Sent)
                continue;

            entry.Moving = false;
            SetVisible(entry, true);
        }
    }

    private void OnDestroy()
    {
        RestoreOldQueue();
    }
}