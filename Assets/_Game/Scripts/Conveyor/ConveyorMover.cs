using System;
using UnityEngine;

public sealed class ConveyorMover : MonoBehaviour
{
    [SerializeField] private ConveyorPath path;

    [SerializeField, Min(0.01f)]
    private float speed = 1.5f;

    [SerializeField]
    private bool startAutomatically;

    public event Action<int, int, int, int> ShotOpportunity;
    public event Action<ConveyorMover> LapCompleted;

    public bool IsMoving { get; private set; }

    private double distance;
    private int nextShotPoint;

    private ShooterController shooter;

    private void Awake()
    {
        speed = GameTuning.Load().MovementSpeed;
        shooter = GetComponent<ShooterController>();
    }

    private void Start()
    {
        if (!startAutomatically)
            return;

        if (path == null)
        {
            Debug.LogError(
                "ConveyorMover: Path atanmamış.",
                this);

            return;
        }

        // Otomatik başlayan shooter da merkezi yönetimden geçer.
        ConveyorManager manager =
            path.GetComponent<ConveyorManager>();

        if (manager == null)
        {
            Debug.LogError(
                "ConveyorMover: Path nesnesinde ConveyorManager yok.",
                this);

            return;
        }

        if (!manager.TryEnter(this))
        {
            Debug.LogWarning(
                "Shooter otomatik olarak banda giremedi.",
                this);
        }
    }

    public void Configure(ConveyorPath conveyorPath)
    {
        path = conveyorPath;
        startAutomatically = false;
    }

    public void BeginLap()
    {
        if (IsMoving || !isActiveAndEnabled)
            return;

        if (path == null || path.Length <= 0f)
            return;

        if (float.IsNaN(speed) ||
            float.IsInfinity(speed) ||
            speed <= 0f)
        {
            Debug.LogError(
                "ConveyorMover: Speed sıfırdan büyük olmalı.",
                this);

            return;
        }

        distance = 0d;
        nextShotPoint = 0;
        IsMoving = true;

        ApplyPose();
    }

    // Sıradaki atış veya tur sonuna kalan süre.
    internal double GetTimeUntilNextEvent()
    {
        if (!IsMoving || !isActiveAndEnabled)
            return double.PositiveInfinity;

        double eventDistance = GetNextEventDistance();

        return Math.Max(0d, eventDistance - distance) / speed;
    }

    // Olay çalıştırmadan yalnızca hareket ettirir.
    internal void AdvanceMovement(double seconds)
    {
        if (!IsMoving || !isActiveAndEnabled)
            return;

        distance = Math.Min(
            path.Length,
            distance + speed * seconds);

        ApplyPose();
    }

    // ConveyorManager tarafından doğru zamanda çağrılır.
    internal void ProcessNextEvent()
    {
        if (!IsMoving || !isActiveAndEnabled)
            return;

        distance = GetNextEventDistance();
        ApplyPose();

        if (nextShotPoint >= path.ShotPointCount)
        {
            CompleteLap();
            return;
        }

        path.GetShotPoint(
            nextShotPoint,
            out _,
            out int startX,
            out int startY,
            out int stepX,
            out int stepY);

        nextShotPoint++;

        ShotOpportunity?.Invoke(
            startX,
            startY,
            stepX,
            stepY);

        // Atış sırasında oyun bitmiş veya nesne kapatılmış olabilir.
        if (!IsMoving || !isActiveAndEnabled)
            return;

        // Mühimmat bittiyse turun kalanını dolaşmasına gerek yok.
        if (shooter != null &&
            shooter.IsReady &&
            shooter.RemainingAmmo <= 0)
        {
            CompleteLap();
        }
    }

    private double GetNextEventDistance()
    {
        if (nextShotPoint >= path.ShotPointCount)
            return path.Length;

        path.GetShotPoint(
            nextShotPoint,
            out float pointDistance,
            out _,
            out _,
            out _,
            out _);

        return pointDistance;
    }

    private void CompleteLap()
    {
        if (!IsMoving)
            return;

        IsMoving = false;
        LapCompleted?.Invoke(this);
    }

    private void ApplyPose()
    {
        if (!path.TryGetPose(
                (float)distance,
                out Vector3 position,
                out Vector3 direction))
        {
            return;
        }

        transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(direction, Vector3.up));
    }

    private void OnDisable()
    {
        IsMoving = false;
    }
}