using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class InputController : MonoBehaviour
{
    [SerializeField] private Camera gameCamera;
    [SerializeField] private WaitingAreaManager waitingArea;
    [SerializeField] private ShooterQueue shooterQueue;
    [SerializeField] private LayerMask shooterLayer;

#if ENABLE_INPUT_SYSTEM
    private InputAction pressAction;
    private bool hasPendingPress;
    private Vector2 pendingPosition;
#endif

    private void Awake()
    {
        if (gameCamera == null ||
            waitingArea == null ||
            shooterQueue == null)
        {
            Debug.LogError(
                "InputController: Kamera, Waiting Area ve Shooter Queue " +
                "alanlarını doldur.",
                this);

            enabled = false;
        }
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (pressAction == null)
        {
            pressAction = new InputAction(
                "Select Shooter",
                InputActionType.Button);

            pressAction.AddBinding("<Mouse>/leftButton");
            pressAction.AddBinding("<Touchscreen>/primaryTouch/press");
        }

        pressAction.performed += HandlePress;
        pressAction.Enable();
#endif
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (!hasPendingPress)
            return;

        hasPendingPress = false;
        TrySelectShooter(pendingPosition);

#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
                TrySelectShooter(touch.position);

            return;
        }

        if (Input.GetMouseButtonDown(0))
            TrySelectShooter(Input.mousePosition);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void HandlePress(InputAction.CallbackContext context)
    {
        if (hasPendingPress)
            return;

        if (context.control.device is Pointer pointer)
        {
            pendingPosition = pointer.position.ReadValue();
            hasPendingPress = true;
        }
    }
#endif

    private void TrySelectShooter(Vector2 screenPosition)
    {
        if (!gameCamera.pixelRect.Contains(screenPosition))
            return;

        Ray ray = gameCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                gameCamera.farClipPlane,
                shooterLayer,
                QueryTriggerInteraction.Collide))
        {
            return;
        }

        ConveyorMover shooter =
            hit.collider.GetComponentInParent<ConveyorMover>();

        if (shooter == null || shooter.IsMoving)
            return;

        if (waitingArea.TrySendShooter(shooter))
            return;

        shooterQueue.TrySendShooter(shooter);
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        hasPendingPress = false;

        if (pressAction != null)
        {
            pressAction.performed -= HandlePress;
            pressAction.Disable();
        }
#endif
    }

    private void OnDestroy()
    {
#if ENABLE_INPUT_SYSTEM
        pressAction?.Dispose();
#endif
    }
}