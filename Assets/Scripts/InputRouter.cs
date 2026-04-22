using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

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

    [Header("Gamepad Tuning")]
    [SerializeField, Min(0f)] private float gamepadStickRotationScale = 1f;
    [SerializeField, Min(0f)] private float gamepadFaceButtonRotationScale = 0.85f;
    [SerializeField, Min(0f)] private float gamepadTiltScale = 0.9f;
    [SerializeField, Min(0f)] private float gamepadDpadMoveScale = 0.85f;
    [SerializeField, Min(0f)] private float gamepadShoulderMoveScale = 0.8f;
    [SerializeField, Min(0f)] private float gamepadTriggerMoveScale = 1f;

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
    public string ActiveDeviceLabel { get; private set; } = "Keyboard / Mouse";
    public string ConnectedGamepadLabel { get; private set; } = "No gamepad detected";
    public bool IsGamepadConnected { get; private set; }
    public bool IsPlayStationStyleGamepadConnected { get; private set; }

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
        UpdateConnectedGamepadStatus(GetConnectedGamepad());

        Vector2 keyboardRotate = keyboardObjectRotateAction.ReadValue<Vector2>();
        Vector2 gamepadRotateStick = ScaleVector(
            ApplyDeadzone(gamepadObjectRotateAction.ReadValue<Vector2>()),
            gamepadStickRotationScale);
        Vector2 gamepadRotateButtons = ScaleVector(
            gamepadObjectRotateButtonsAction.ReadValue<Vector2>(),
            gamepadFaceButtonRotationScale);
        Vector2 gamepadRotate = CombineNormalized(gamepadRotateStick, gamepadRotateButtons);
        ObjectRotation = CombineNormalized(keyboardRotate, gamepadRotate);

        Vector2 keyboardMove = keyboardMoveAction.ReadValue<Vector2>();
        Vector2 gamepadMovePad = ScaleVector(
            gamepadMoveAction.ReadValue<Vector2>(),
            gamepadDpadMoveScale);
        Vector2 gamepadMoveButtons = new Vector2(
            gamepadMoveLateralButtonsAction.ReadValue<float>() * gamepadShoulderMoveScale,
            ApplyTriggerDeadzone(gamepadMoveDepthButtonsAction.ReadValue<float>()) * gamepadTriggerMoveScale);
        Vector2 gamepadMove = CombineNormalized(gamepadMovePad, gamepadMoveButtons);
        CameraTranslation = CombineNormalized(keyboardMove, gamepadMove);

        Vector2 mouseTilt = Vector2.zero;
        if (!requireRightMouseForTilt || rightMouseAction.IsPressed())
        {
            mouseTilt = NormalizeMouse(mouseTiltAction.ReadValue<Vector2>());
        }

        Vector2 gamepadTilt = ScaleVector(
            ApplyDeadzone(gamepadTiltAction.ReadValue<Vector2>()),
            gamepadTiltScale);
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
            ActiveDeviceLabel = ConnectedGamepadLabel;
        }
        else if (keyboardMouseMagnitude > 0.001f)
        {
            ActiveInput = ActiveInputMode.KeyboardMouse;
            ActiveDeviceLabel = mouseTilt.magnitude > 0.001f ? "Mouse + Keyboard" : "Keyboard";
        }
        else if (!IsGamepadConnected)
        {
            ActiveDeviceLabel = "Keyboard / Mouse";
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

    private static Vector2 ScaleVector(Vector2 input, float scale)
    {
        return Vector2.ClampMagnitude(input * scale, 1f);
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

    private Gamepad GetConnectedGamepad()
    {
        if (Gamepad.current != null)
        {
            return Gamepad.current;
        }

        if (DualShockGamepad.current != null)
        {
            return DualShockGamepad.current;
        }

        return Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
    }

    private void UpdateConnectedGamepadStatus(Gamepad gamepad)
    {
        IsGamepadConnected = gamepad != null;
        IsPlayStationStyleGamepadConnected = gamepad != null && IsPlayStationStyleGamepad(gamepad);
        ConnectedGamepadLabel = FormatGamepadLabel(gamepad);
    }

    private string FormatGamepadLabel(Gamepad gamepad)
    {
        if (gamepad == null)
        {
            return "No gamepad detected";
        }

        string rawName = !string.IsNullOrWhiteSpace(gamepad.displayName)
            ? gamepad.displayName
            : gamepad.description.product;

        if (string.IsNullOrWhiteSpace(rawName))
        {
            rawName = gamepad.layout;
        }

        if (IsPlayStationStyleGamepad(gamepad))
        {
            return $"PlayStation-style ({rawName})";
        }

        return rawName;
    }

    private bool IsPlayStationStyleGamepad(Gamepad gamepad)
    {
        if (gamepad is DualShockGamepad)
        {
            return true;
        }

        string deviceInfo = string.Concat(
            gamepad.displayName, " ",
            gamepad.description.product, " ",
            gamepad.description.manufacturer, " ",
            gamepad.layout);

        return deviceInfo.IndexOf("playstation", StringComparison.OrdinalIgnoreCase) >= 0
            || deviceInfo.IndexOf("dualshock", StringComparison.OrdinalIgnoreCase) >= 0
            || deviceInfo.IndexOf("wireless controller", StringComparison.OrdinalIgnoreCase) >= 0
            || deviceInfo.IndexOf("sony", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Future DualShock / DualSense gyro data should be merged here so CameraMover keeps consuming one clean tilt signal.
    // For this prototype, keep gyro optional and isolated to the input layer.
    private Vector2 ReadGyroTiltExtension()
    {
        return Vector2.zero;
    }
}
