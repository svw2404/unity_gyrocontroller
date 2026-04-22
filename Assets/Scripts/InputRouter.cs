using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class InputRouter : MonoBehaviour
{
    public enum ActiveInputMode
    {
        KeyboardMouse,
        Gamepad
    }

    [Header("Mouse Tilt")]
    [SerializeField] private bool requireRightMouseForTilt = true;
    [SerializeField] private bool lockCursorWhileTilting = true;
    [SerializeField, Range(0.001f, 0.05f)] private float mouseDeltaNormalization = 0.01f;

    [Header("Filtering")]
    [SerializeField, Range(0f, 0.5f)] private float stickDeadzone = 0.15f;
    [SerializeField, Range(0f, 0.25f)] private float triggerDeadzone = 0.1f;

    private InputAction keyboardObjectRotateAction;
    private InputAction gamepadObjectRotateAction;
    private InputAction gamepadObjectRotateButtonsAction;
    private InputAction keyboardMoveAction;
    private InputAction gamepadMoveAction;
    private InputAction gamepadMoveLateralButtonsAction;
    private InputAction gamepadMoveDepthButtonsAction;
    private InputAction mouseTiltAction;
    private InputAction gamepadTiltAction;
    private InputAction rightMouseAction;

    public Vector2 ObjectRotation { get; private set; }
    public Vector2 CameraTilt { get; private set; }
    public Vector2 CameraTranslation { get; private set; }
    public float MotionMagnitude { get; private set; }
    public ActiveInputMode ActiveInput { get; private set; }
    public string ActiveInputLabel => ActiveInput == ActiveInputMode.Gamepad ? "Gamepad" : "Keyboard / Mouse";

    private void Awake()
    {
        CreateActionsIfNeeded();
    }

    private void OnEnable()
    {
        CreateActionsIfNeeded();
        EnableActions();
    }

    private void OnDisable()
    {
        DisableActions();
        ReleaseCursor();
    }

    private void OnDestroy()
    {
        DisposeActions();
    }

    private void Update()
    {
        UpdateCursorState();
        UpdateOutputs();
    }

    private void CreateActionsIfNeeded()
    {
        if (keyboardObjectRotateAction != null)
        {
            return;
        }

        keyboardObjectRotateAction = new InputAction("KeyboardObjectRotate", InputActionType.Value);
        keyboardObjectRotateAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        gamepadObjectRotateAction = new InputAction(
            "GamepadObjectRotate",
            InputActionType.Value,
            "<Gamepad>/leftStick");

        gamepadObjectRotateButtonsAction = new InputAction("GamepadObjectRotateButtons", InputActionType.Value);
        gamepadObjectRotateButtonsAction.AddCompositeBinding("2DVector")
            .With("Up", "<Gamepad>/buttonNorth")
            .With("Down", "<Gamepad>/buttonSouth")
            .With("Left", "<Gamepad>/buttonWest")
            .With("Right", "<Gamepad>/buttonEast");

        keyboardMoveAction = new InputAction("KeyboardMove", InputActionType.Value);
        keyboardMoveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        gamepadMoveAction = new InputAction(
            "GamepadMove",
            InputActionType.Value,
            "<Gamepad>/dpad");

        gamepadMoveLateralButtonsAction = new InputAction("GamepadMoveLateralButtons", InputActionType.Value);
        gamepadMoveLateralButtonsAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Gamepad>/leftShoulder")
            .With("Positive", "<Gamepad>/rightShoulder");

        gamepadMoveDepthButtonsAction = new InputAction("GamepadMoveDepthButtons", InputActionType.Value);
        gamepadMoveDepthButtonsAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Gamepad>/leftTrigger")
            .With("Positive", "<Gamepad>/rightTrigger");

        mouseTiltAction = new InputAction(
            "MouseTilt",
            InputActionType.PassThrough,
            "<Mouse>/delta");

        gamepadTiltAction = new InputAction(
            "GamepadTilt",
            InputActionType.Value,
            "<Gamepad>/rightStick");

        rightMouseAction = new InputAction(
            "RightMouseHold",
            InputActionType.Button,
            "<Mouse>/rightButton");
    }

    private void EnableActions()
    {
        keyboardObjectRotateAction.Enable();
        gamepadObjectRotateAction.Enable();
        gamepadObjectRotateButtonsAction.Enable();
        keyboardMoveAction.Enable();
        gamepadMoveAction.Enable();
        gamepadMoveLateralButtonsAction.Enable();
        gamepadMoveDepthButtonsAction.Enable();
        mouseTiltAction.Enable();
        gamepadTiltAction.Enable();
        rightMouseAction.Enable();
    }

    private void DisableActions()
    {
        keyboardObjectRotateAction?.Disable();
        gamepadObjectRotateAction?.Disable();
        gamepadObjectRotateButtonsAction?.Disable();
        keyboardMoveAction?.Disable();
        gamepadMoveAction?.Disable();
        gamepadMoveLateralButtonsAction?.Disable();
        gamepadMoveDepthButtonsAction?.Disable();
        mouseTiltAction?.Disable();
        gamepadTiltAction?.Disable();
        rightMouseAction?.Disable();
    }

    private void DisposeActions()
    {
        keyboardObjectRotateAction?.Dispose();
        gamepadObjectRotateAction?.Dispose();
        gamepadObjectRotateButtonsAction?.Dispose();
        keyboardMoveAction?.Dispose();
        gamepadMoveAction?.Dispose();
        gamepadMoveLateralButtonsAction?.Dispose();
        gamepadMoveDepthButtonsAction?.Dispose();
        mouseTiltAction?.Dispose();
        gamepadTiltAction?.Dispose();
        rightMouseAction?.Dispose();
    }

    private void UpdateCursorState()
    {
        if (!lockCursorWhileTilting || Mouse.current == null)
        {
            return;
        }

        bool shouldLockCursor = !requireRightMouseForTilt || rightMouseAction.IsPressed();

        if (shouldLockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        ReleaseCursor();
    }

    private void UpdateOutputs()
    {
        Vector2 keyboardRotate = keyboardObjectRotateAction.ReadValue<Vector2>();
        Vector2 gamepadRotateStick = ApplyDeadzone(gamepadObjectRotateAction.ReadValue<Vector2>());
        Vector2 gamepadRotateButtons = gamepadObjectRotateButtonsAction.ReadValue<Vector2>();
        Vector2 gamepadRotate = CombineNormalized(gamepadRotateStick, gamepadRotateButtons);
        ObjectRotation = CombineNormalized(keyboardRotate, gamepadRotate);

        Vector2 keyboardMove = keyboardMoveAction.ReadValue<Vector2>();
        Vector2 gamepadMovePad = gamepadMoveAction.ReadValue<Vector2>();
        Vector2 gamepadMoveButtons = new Vector2(
            gamepadMoveLateralButtonsAction.ReadValue<float>(),
            ApplyTriggerDeadzone(gamepadMoveDepthButtonsAction.ReadValue<float>()));
        Vector2 gamepadMove = CombineNormalized(gamepadMovePad, gamepadMoveButtons);
        CameraTranslation = CombineNormalized(keyboardMove, gamepadMove);

        Vector2 mouseTilt = Vector2.zero;
        if (!requireRightMouseForTilt || rightMouseAction.IsPressed())
        {
            mouseTilt = NormalizeMouse(mouseTiltAction.ReadValue<Vector2>());
        }

        Vector2 gamepadTilt = ApplyDeadzone(gamepadTiltAction.ReadValue<Vector2>());
        Vector2 gyroTilt = ReadGyroTiltExtension();
        CameraTilt = CombineNormalized(mouseTilt, gamepadTilt, gyroTilt);

        float keyboardMouseMagnitude = Mathf.Max(
            keyboardRotate.magnitude,
            Mathf.Max(keyboardMove.magnitude, mouseTilt.magnitude));
        float gamepadMagnitude = Mathf.Max(
            gamepadRotate.magnitude,
            Mathf.Max(gamepadMove.magnitude, Mathf.Max(gamepadTilt.magnitude, gyroTilt.magnitude)));

        if (gamepadMagnitude > 0.001f)
        {
            ActiveInput = ActiveInputMode.Gamepad;
        }
        else if (keyboardMouseMagnitude > 0.001f)
        {
            ActiveInput = ActiveInputMode.KeyboardMouse;
        }

        MotionMagnitude = Mathf.Clamp01(
            Mathf.Max(ObjectRotation.magnitude, Mathf.Max(CameraTilt.magnitude, CameraTranslation.magnitude)));
    }

    private Vector2 NormalizeMouse(Vector2 rawMouseDelta)
    {
        Vector2 normalized = rawMouseDelta * mouseDeltaNormalization;
        normalized.x = Mathf.Clamp(normalized.x, -1f, 1f);
        normalized.y = Mathf.Clamp(normalized.y, -1f, 1f);
        return normalized;
    }

    private Vector2 ApplyDeadzone(Vector2 rawInput)
    {
        float magnitude = rawInput.magnitude;
        if (magnitude <= stickDeadzone)
        {
            return Vector2.zero;
        }

        float scaledMagnitude = Mathf.InverseLerp(stickDeadzone, 1f, magnitude);
        return rawInput.normalized * scaledMagnitude;
    }

    private float ApplyTriggerDeadzone(float rawInput)
    {
        float absValue = Mathf.Abs(rawInput);
        if (absValue <= triggerDeadzone)
        {
            return 0f;
        }

        float scaled = Mathf.InverseLerp(triggerDeadzone, 1f, absValue);
        return Mathf.Sign(rawInput) * scaled;
    }

    private static Vector2 CombineNormalized(Vector2 first, Vector2 second)
    {
        return Vector2.ClampMagnitude(first + second, 1f);
    }

    private static Vector2 CombineNormalized(Vector2 first, Vector2 second, Vector2 third)
    {
        return Vector2.ClampMagnitude(first + second + third, 1f);
    }

    private void ReleaseCursor()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        Cursor.visible = true;
    }

    // Future DualShock / DualSense gyro data should be merged here so CameraMover keeps consuming one clean tilt signal.
    private Vector2 ReadGyroTiltExtension()
    {
        return Vector2.zero;
    }
}
