using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using InputSystemAccelerometer = UnityEngine.InputSystem.Accelerometer;
using InputSystemAttitudeSensor = UnityEngine.InputSystem.AttitudeSensor;
using InputSystemGyroscope = UnityEngine.InputSystem.Gyroscope;
using InputSystemSensor = UnityEngine.InputSystem.Sensor;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class InputRouter : MonoBehaviour
{
    private static readonly Quaternion LegacyGyroRotationFix = new Quaternion(0f, 0f, 1f, 0f);

    public enum InputMode
    {
        Auto,
        KeyboardMouse,
        PS4Gamepad,
        PhoneMotion
    }

    [Header("Mode")]
    [SerializeField] private InputMode inputMode = InputMode.Auto;

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

    [Header("Phone Motion")]
    [SerializeField] private bool enablePhoneMotion = true;
    [SerializeField] private bool autoEnableAvailableSensors = true;
    [SerializeField] private bool autoCalibratePhoneReference = true;
    [SerializeField, Min(0f)] private float phoneSensorSamplingFrequency = 60f;
    [SerializeField, Min(1f)] private float phoneMaxYawDegrees = 35f;
    [SerializeField, Min(1f)] private float phoneMaxPitchDegrees = 30f;
    [SerializeField, Range(0f, 10f)] private float phoneYawDeadzoneDegrees = 1.25f;
    [SerializeField, Range(0f, 10f)] private float phonePitchDeadzoneDegrees = 1.25f;
    [SerializeField, Min(0f)] private float phoneObjectRotationScale = 1f;
    [SerializeField, Min(0f)] private float phoneAngularVelocityDeadzone = 0.12f;
    [SerializeField, Min(0f)] private float phoneAccelerometerIntensityScale = 0.18f;
    [SerializeField, Min(0f)] private float phoneAccelerationDeadzone = 0.08f;
    [SerializeField, Min(0f)] private float phoneAngularVelocityIntensityScale = 0.18f;
    [SerializeField, Range(0f, 1f)] private float phonePoseIntensityWeight = 0.35f;
    [SerializeField, Min(0.01f)] private float phoneOutputSmoothTime = 0.1f;

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

    private InputSystemAttitudeSensor attitudeSensor;
    private InputSystemGyroscope gyroscope;
    private InputSystemAccelerometer accelerometer;

    private Quaternion phoneReferenceAttitude = Quaternion.identity;
    private bool hasPhoneReference;
    private float phoneAccelerationBaseline = -1f;
    private Vector2 phoneObjectRotationVelocity;
    private Vector2 phoneCameraTiltVelocity;
    private float phoneMotionVelocity;

    public Vector2 ObjectRotation { get; private set; }
    public Vector2 CameraTilt { get; private set; }
    public Vector2 CameraTranslation { get; private set; }
    public float MotionMagnitude { get; private set; }

    public InputMode ConfiguredInputMode => inputMode;
    public InputMode CurrentInputMode { get; private set; } = InputMode.KeyboardMouse;
    public string ConfiguredInputModeLabel => GetInputModeLabel(ConfiguredInputMode);
    public string CurrentInputModeLabel => GetInputModeLabel(CurrentInputMode);
    public string ActiveInputLabel => CurrentInputModeLabel;
    public bool IsUsingPhoneMotion => CurrentInputMode == InputMode.PhoneMotion;
    public string ControlPathLabel => IsUsingPhoneMotion ? "Phone Motion -> Cube / Visuals (Camera Locked)" : "Fallback Controls Active";
    public string ActiveDeviceLabel { get; private set; } = "Keyboard / Mouse";
    public string ConnectedGamepadLabel { get; private set; } = "No gamepad detected";

    public bool IsGamepadConnected { get; private set; }
    public bool IsPlayStationStyleGamepadConnected { get; private set; }
    public bool IsPhoneMotionEnabled => enablePhoneMotion;
    public bool AreMobileSensorsDetected { get; private set; }
    public bool IsAttitudeSensorDetected => attitudeSensor != null;
    public bool IsGyroscopeDetected => gyroscope != null;
    public bool IsAccelerometerDetected => accelerometer != null;
    public bool IsAttitudeSensorEnabled => attitudeSensor != null && attitudeSensor.enabled;
    public bool IsGyroscopeEnabled => gyroscope != null && gyroscope.enabled;
    public bool IsAccelerometerEnabled => accelerometer != null && accelerometer.enabled;
    public bool IsLegacyGyroAvailable => SystemInfo.supportsGyroscope;
    public bool IsLegacyGyroEnabled => enablePhoneMotion && SystemInfo.supportsGyroscope && Input.gyro.enabled;
    public Vector2 PhoneObjectRotation { get; private set; }
    public Vector2 PhoneCameraTilt { get; private set; }
    public float PhoneMotionMagnitude { get; private set; }
    public string MobileSensorLabel => BuildMobileSensorLabel();

    private void Awake()
    {
        CreateActionsIfNeeded();
        RefreshPhoneSensorDevices();
    }

    private void OnEnable()
    {
        CreateActionsIfNeeded();
        EnableActions();
        RefreshPhoneSensorDevices();
        EnableAvailablePhoneSensors();
        EnableLegacyPhoneGyro();

        if (autoCalibratePhoneReference)
        {
            ResetPhoneReference();
        }
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
        RefreshPhoneSensorDevices();
        EnableAvailablePhoneSensors();
        EnableLegacyPhoneGyro();
        UpdateConnectedGamepadStatus(GetConnectedGamepad());

        Vector2 keyboardRotate = keyboardObjectRotateAction.ReadValue<Vector2>();
        Vector2 gamepadRotateStick = ScaleVector(
            ApplyDeadzone(gamepadObjectRotateAction.ReadValue<Vector2>()),
            gamepadStickRotationScale);
        Vector2 gamepadRotateButtons = ScaleVector(
            gamepadObjectRotateButtonsAction.ReadValue<Vector2>(),
            gamepadFaceButtonRotationScale);
        Vector2 gamepadRotate = CombineNormalized(gamepadRotateStick, gamepadRotateButtons);

        Vector2 keyboardMove = keyboardMoveAction.ReadValue<Vector2>();
        Vector2 gamepadMovePad = ScaleVector(
            gamepadMoveAction.ReadValue<Vector2>(),
            gamepadDpadMoveScale);
        Vector2 gamepadMoveButtons = new Vector2(
            gamepadMoveLateralButtonsAction.ReadValue<float>() * gamepadShoulderMoveScale,
            ApplyTriggerDeadzone(gamepadMoveDepthButtonsAction.ReadValue<float>()) * gamepadTriggerMoveScale);
        Vector2 gamepadMove = CombineNormalized(gamepadMovePad, gamepadMoveButtons);

        Vector2 mouseTilt = Vector2.zero;
        if (!requireRightMouseForTilt || rightMouseAction.IsPressed())
        {
            mouseTilt = NormalizeMouse(mouseTiltAction.ReadValue<Vector2>());
        }

        Vector2 gamepadTilt = ScaleVector(
            ApplyDeadzone(gamepadTiltAction.ReadValue<Vector2>()),
            gamepadTiltScale);
        Vector2 controllerGyroTilt = ReadGyroTiltExtension();

        UpdatePhoneMotionOutputs();

        ObjectRotation = ResolveObjectRotation(keyboardRotate, gamepadRotate, PhoneObjectRotation);
        CameraTilt = ResolveCameraTilt(mouseTilt, gamepadTilt, controllerGyroTilt);
        CameraTranslation = ResolveCameraTranslation(keyboardMove, gamepadMove);

        float keyboardMouseMagnitude = Mathf.Max(
            keyboardRotate.magnitude,
            Mathf.Max(keyboardMove.magnitude, mouseTilt.magnitude));
        float gamepadMagnitude = Mathf.Max(
            gamepadRotate.magnitude,
            Mathf.Max(gamepadMove.magnitude, Mathf.Max(gamepadTilt.magnitude, controllerGyroTilt.magnitude)));
        float phoneMagnitude = Mathf.Max(
            PhoneObjectRotation.magnitude,
            Mathf.Max(PhoneCameraTilt.magnitude, PhoneMotionMagnitude));

        CurrentInputMode = ResolveCurrentInputMode(keyboardMouseMagnitude, gamepadMagnitude, phoneMagnitude);
        ActiveDeviceLabel = ResolveActiveDeviceLabel(CurrentInputMode, mouseTilt.magnitude);

        MotionMagnitude = ResolveMotionMagnitude(keyboardRotate, keyboardMove, mouseTilt, gamepadRotate, gamepadMove, gamepadTilt);
    }

    private Vector2 ResolveObjectRotation(Vector2 keyboardRotate, Vector2 gamepadRotate, Vector2 phoneRotate)
    {
        switch (inputMode)
        {
            case InputMode.KeyboardMouse:
                return keyboardRotate;
            case InputMode.PS4Gamepad:
                return CombineNormalized(gamepadRotate, keyboardRotate);
            case InputMode.PhoneMotion:
                if (HasUsablePhoneMotion())
                {
                    return CombineNormalized(phoneRotate, CombineNormalized(gamepadRotate, keyboardRotate));
                }

                return CombineNormalized(gamepadRotate, keyboardRotate);
            default:
                return CombineNormalized(phoneRotate, CombineNormalized(keyboardRotate, gamepadRotate));
        }
    }

    private Vector2 ResolveCameraTilt(Vector2 mouseTilt, Vector2 gamepadTilt, Vector2 controllerGyroTilt)
    {
        Vector2 fallbackTilt = CombineNormalized(mouseTilt, gamepadTilt, controllerGyroTilt);

        switch (inputMode)
        {
            case InputMode.KeyboardMouse:
                return mouseTilt;
            case InputMode.PS4Gamepad:
                return CombineNormalized(gamepadTilt, CombineNormalized(controllerGyroTilt, mouseTilt));
            case InputMode.PhoneMotion:
                if (HasUsablePhoneMotion())
                {
                    return Vector2.zero;
                }

                return fallbackTilt;
            default:
                return fallbackTilt;
        }
    }

    private Vector2 ResolveCameraTranslation(Vector2 keyboardMove, Vector2 gamepadMove)
    {
        switch (inputMode)
        {
            case InputMode.KeyboardMouse:
                return keyboardMove;
            case InputMode.PS4Gamepad:
                return CombineNormalized(gamepadMove, keyboardMove);
            case InputMode.PhoneMotion:
                if (HasUsablePhoneMotion())
                {
                    return Vector2.zero;
                }

                return CombineNormalized(keyboardMove, gamepadMove);
            case InputMode.Auto:
            default:
                return CombineNormalized(keyboardMove, gamepadMove);
        }
    }

    private float ResolveMotionMagnitude(
        Vector2 keyboardRotate,
        Vector2 keyboardMove,
        Vector2 mouseTilt,
        Vector2 gamepadRotate,
        Vector2 gamepadMove,
        Vector2 gamepadTilt)
    {
        float keyboardMouseMagnitude = Mathf.Max(
            keyboardRotate.magnitude,
            Mathf.Max(keyboardMove.magnitude, mouseTilt.magnitude));
        float gamepadMagnitude = Mathf.Max(
            gamepadRotate.magnitude,
            Mathf.Max(gamepadMove.magnitude, gamepadTilt.magnitude));
        float phonePoseMagnitude = Mathf.Max(PhoneObjectRotation.magnitude, PhoneCameraTilt.magnitude) * phonePoseIntensityWeight;

        switch (inputMode)
        {
            case InputMode.KeyboardMouse:
                return Mathf.Clamp01(keyboardMouseMagnitude);
            case InputMode.PS4Gamepad:
                return Mathf.Clamp01(Mathf.Max(gamepadMagnitude, keyboardMouseMagnitude));
            case InputMode.PhoneMotion:
                if (HasUsablePhoneMotion())
                {
                    return Mathf.Clamp01(Mathf.Max(PhoneMotionMagnitude, Mathf.Max(phonePoseMagnitude, Mathf.Max(gamepadMagnitude, keyboardMouseMagnitude))));
                }

                return Mathf.Clamp01(Mathf.Max(gamepadMagnitude, keyboardMouseMagnitude));
            default:
                return Mathf.Clamp01(Mathf.Max(
                    PhoneMotionMagnitude,
                    Mathf.Max(phonePoseMagnitude, Mathf.Max(gamepadMagnitude, keyboardMouseMagnitude))));
        }
    }

    private void UpdatePhoneMotionOutputs()
    {
        Vector2 targetPhoneObjectRotation = Vector2.zero;
        Vector2 targetPhoneCameraTilt = Vector2.zero;
        float targetPhoneMotionMagnitude = 0f;

        if (enablePhoneMotion)
        {
            Vector3 relativePhoneAngles;
            bool hasAttitude = TryGetRelativePhoneAngles(out relativePhoneAngles);

            if (hasAttitude)
            {
                // Phone orientation now drives the object directly; camera tilt stays on the existing fallback controls.
                Vector2 normalizedPhoneRotation = ApplyPhoneRotationDeadzone(
                    new Vector2(
                        NormalizeAngleToInput(relativePhoneAngles.y, phoneMaxYawDegrees),
                        NormalizeAngleToInput(relativePhoneAngles.x, phoneMaxPitchDegrees)));
                targetPhoneObjectRotation = ScaleVector(
                    normalizedPhoneRotation,
                    phoneObjectRotationScale);
            }
            else
            {
                hasPhoneReference = false;
            }

            if (IsGyroscopeEnabled)
            {
                Vector3 angularVelocity = gyroscope.angularVelocity.ReadValue();
                targetPhoneMotionMagnitude = Mathf.Max(
                    targetPhoneMotionMagnitude,
                    Mathf.Clamp01(
                        ApplyPositiveDeadzone(angularVelocity.magnitude, phoneAngularVelocityDeadzone) *
                        phoneAngularVelocityIntensityScale));
            }
            else if (IsLegacyGyroEnabled)
            {
                Vector3 angularVelocity = Input.gyro.rotationRateUnbiased;
                targetPhoneMotionMagnitude = Mathf.Max(
                    targetPhoneMotionMagnitude,
                    Mathf.Clamp01(
                        ApplyPositiveDeadzone(angularVelocity.magnitude, phoneAngularVelocityDeadzone) *
                        phoneAngularVelocityIntensityScale));
            }

            if (accelerometer != null)
            {
                Vector3 acceleration = accelerometer.acceleration.ReadValue();
                float accelerationMagnitude = acceleration.magnitude;
                if (phoneAccelerationBaseline < 0f)
                {
                    phoneAccelerationBaseline = accelerationMagnitude;
                }

                float accelerationDelta = Mathf.Abs(accelerationMagnitude - phoneAccelerationBaseline);
                targetPhoneMotionMagnitude = Mathf.Max(
                    targetPhoneMotionMagnitude,
                    Mathf.Clamp01(
                        ApplyPositiveDeadzone(accelerationDelta, phoneAccelerationDeadzone) *
                        phoneAccelerometerIntensityScale));

                if (accelerationDelta <= phoneAccelerationDeadzone * 0.5f)
                {
                    phoneAccelerationBaseline = Mathf.Lerp(phoneAccelerationBaseline, accelerationMagnitude, 0.08f);
                }
            }

            targetPhoneMotionMagnitude = Mathf.Max(
                targetPhoneMotionMagnitude,
                Mathf.Max(targetPhoneObjectRotation.magnitude, targetPhoneCameraTilt.magnitude) * phonePoseIntensityWeight);
        }
        else
        {
            hasPhoneReference = false;
        }

        PhoneObjectRotation = Vector2.ClampMagnitude(
            Vector2.SmoothDamp(
                PhoneObjectRotation,
                targetPhoneObjectRotation,
                ref phoneObjectRotationVelocity,
                phoneOutputSmoothTime),
            1f);

        PhoneCameraTilt = Vector2.ClampMagnitude(
            Vector2.SmoothDamp(
                PhoneCameraTilt,
                targetPhoneCameraTilt,
                ref phoneCameraTiltVelocity,
                phoneOutputSmoothTime),
            1f);

        PhoneMotionMagnitude = Mathf.Clamp01(
            Mathf.SmoothDamp(
                PhoneMotionMagnitude,
                targetPhoneMotionMagnitude,
                ref phoneMotionVelocity,
                phoneOutputSmoothTime));
    }

    private void RefreshPhoneSensorDevices()
    {
        attitudeSensor = InputSystemAttitudeSensor.current;
        gyroscope = InputSystemGyroscope.current;
        accelerometer = InputSystemAccelerometer.current;

        AreMobileSensorsDetected = attitudeSensor != null || gyroscope != null || accelerometer != null || IsLegacyGyroAvailable;
    }

    private void EnableAvailablePhoneSensors()
    {
        if (!enablePhoneMotion || !autoEnableAvailableSensors)
        {
            return;
        }

        // Unity Remote exposes the remote phone sensors as Input System devices in the Editor.
        // Attitude and gyroscope are explicitly enabled here so the rest of the prototype can stay device-agnostic.
        TryEnableSensor(attitudeSensor);
        TryEnableSensor(gyroscope);
        TryEnableSensor(accelerometer);
    }

    private void EnableLegacyPhoneGyro()
    {
        if (!enablePhoneMotion || !SystemInfo.supportsGyroscope)
        {
            return;
        }

        if (!Input.gyro.enabled)
        {
            Input.gyro.enabled = true;
        }
    }

    private void TryEnableSensor(InputSystemSensor sensor)
    {
        if (sensor == null)
        {
            return;
        }

        if (!sensor.enabled)
        {
            InputSystem.EnableDevice(sensor);
        }

        if (phoneSensorSamplingFrequency <= 0f)
        {
            return;
        }

        try
        {
            sensor.samplingFrequency = phoneSensorSamplingFrequency;
        }
        catch (NotSupportedException)
        {
            // Some sensor backends do not expose sampling frequency control.
        }
    }

    // Treat the current phone pose as neutral so phone motion drives relative correspondence instead of absolute tracking.
    private void ResetPhoneReference()
    {
        hasPhoneReference = false;
        phoneReferenceAttitude = Quaternion.identity;
        phoneAccelerationBaseline = -1f;
        PhoneObjectRotation = Vector2.zero;
        PhoneCameraTilt = Vector2.zero;
        PhoneMotionMagnitude = 0f;
        phoneObjectRotationVelocity = Vector2.zero;
        phoneCameraTiltVelocity = Vector2.zero;
        phoneMotionVelocity = 0f;
    }

    private bool TryGetRelativePhoneAngles(out Vector3 relativePhoneAngles)
    {
        relativePhoneAngles = Vector3.zero;

        Quaternion currentAttitude;
        if (!TryReadPhoneAttitude(out currentAttitude))
        {
            return false;
        }

        if (!hasPhoneReference)
        {
            phoneReferenceAttitude = currentAttitude;
            hasPhoneReference = true;
        }

        Quaternion relativeRotation = Quaternion.Inverse(phoneReferenceAttitude) * currentAttitude;
        Vector3 eulerAngles = relativeRotation.eulerAngles;
        relativePhoneAngles = new Vector3(
            NormalizeSignedAngle(eulerAngles.x),
            NormalizeSignedAngle(eulerAngles.y),
            NormalizeSignedAngle(eulerAngles.z));
        return true;
    }

    private bool TryReadPhoneAttitude(out Quaternion currentAttitude)
    {
        currentAttitude = Quaternion.identity;

        if (!enablePhoneMotion)
        {
            return false;
        }

        if (IsAttitudeSensorEnabled)
        {
            currentAttitude = attitudeSensor.attitude.ReadValue();
            return true;
        }

        if (IsLegacyGyroEnabled)
        {
            currentAttitude = LegacyGyroRotationFix * Input.gyro.attitude;
            return true;
        }

        return false;
    }

    private InputMode ResolveCurrentInputMode(float keyboardMouseMagnitude, float gamepadMagnitude, float phoneMagnitude)
    {
        switch (inputMode)
        {
            case InputMode.KeyboardMouse:
                return InputMode.KeyboardMouse;
            case InputMode.PS4Gamepad:
                if (gamepadMagnitude > 0.001f || IsGamepadConnected)
                {
                    return InputMode.PS4Gamepad;
                }

                return InputMode.KeyboardMouse;
            case InputMode.PhoneMotion:
                if (HasUsablePhoneMotion())
                {
                    return InputMode.PhoneMotion;
                }

                if (gamepadMagnitude > 0.001f || IsGamepadConnected)
                {
                    return InputMode.PS4Gamepad;
                }

                return InputMode.KeyboardMouse;
            default:
                if (phoneMagnitude > gamepadMagnitude && phoneMagnitude > keyboardMouseMagnitude)
                {
                    return InputMode.PhoneMotion;
                }

                if (gamepadMagnitude > keyboardMouseMagnitude)
                {
                    return InputMode.PS4Gamepad;
                }

                if (HasUsablePhoneMotion())
                {
                    return InputMode.PhoneMotion;
                }

                if (IsGamepadConnected)
                {
                    return InputMode.PS4Gamepad;
                }

                return InputMode.KeyboardMouse;
        }
    }

    private string ResolveActiveDeviceLabel(InputMode currentMode, float mouseTiltMagnitude)
    {
        switch (currentMode)
        {
            case InputMode.PS4Gamepad:
                return ConnectedGamepadLabel;
            case InputMode.PhoneMotion:
                if (IsAttitudeSensorEnabled && IsGyroscopeEnabled)
                {
                    return "Phone Motion (Attitude)";
                }

                if (IsAttitudeSensorEnabled)
                {
                    return "Phone Motion (Attitude)";
                }

                if (IsLegacyGyroEnabled)
                {
                    return "Phone Motion (Unity Remote Gyro)";
                }

                if (IsGyroscopeEnabled)
                {
                    return "Phone Motion (Gyro)";
                }

                return "Phone Motion";
            default:
                return mouseTiltMagnitude > 0.001f ? "Mouse + Keyboard" : "Keyboard";
        }
    }

    private bool HasUsablePhoneMotion()
    {
        return enablePhoneMotion && (IsAttitudeSensorEnabled || IsGyroscopeEnabled || IsAccelerometerEnabled || IsLegacyGyroEnabled);
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

    private static float NormalizeAngleToInput(float angle, float maxAngle)
    {
        if (maxAngle <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp(angle / maxAngle, -1f, 1f);
    }

    private Vector2 ApplyPhoneRotationDeadzone(Vector2 normalizedPhoneRotation)
    {
        float yawDeadzone = phoneMaxYawDegrees <= 0f ? 0f : Mathf.Clamp01(phoneYawDeadzoneDegrees / phoneMaxYawDegrees);
        float pitchDeadzone = phoneMaxPitchDegrees <= 0f ? 0f : Mathf.Clamp01(phonePitchDeadzoneDegrees / phoneMaxPitchDegrees);

        return new Vector2(
            ApplySignedDeadzone(normalizedPhoneRotation.x, yawDeadzone),
            ApplySignedDeadzone(normalizedPhoneRotation.y, pitchDeadzone));
    }

    private static float ApplySignedDeadzone(float value, float deadzone)
    {
        float absolute = Mathf.Abs(value);
        if (absolute <= deadzone)
        {
            return 0f;
        }

        float scaled = Mathf.InverseLerp(deadzone, 1f, absolute);
        return Mathf.Sign(value) * scaled;
    }

    private static float ApplyPositiveDeadzone(float value, float deadzone)
    {
        if (value <= deadzone)
        {
            return 0f;
        }

        return value - deadzone;
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

    private string BuildMobileSensorLabel()
    {
        if (!enablePhoneMotion)
        {
            return "Phone motion disabled";
        }

        if (!AreMobileSensorsDetected)
        {
            return "No mobile sensors detected";
        }

        return
            $"Attitude {(IsAttitudeSensorDetected ? "Present" : "Missing")}/{(IsAttitudeSensorEnabled ? "Enabled" : "Disabled")} | " +
            $"Gyro {(IsGyroscopeDetected ? "Present" : "Missing")}/{(IsGyroscopeEnabled ? "Enabled" : "Disabled")} | " +
            $"LegacyGyro {(IsLegacyGyroAvailable ? "Present" : "Missing")}/{(IsLegacyGyroEnabled ? "Enabled" : "Disabled")} | " +
            $"Accel {(IsAccelerometerDetected ? "Present" : "Missing")}/{(IsAccelerometerEnabled ? "Enabled" : "Disabled")}";
    }

    private static string GetInputModeLabel(InputMode mode)
    {
        switch (mode)
        {
            case InputMode.KeyboardMouse:
                return "Keyboard / Mouse";
            case InputMode.PS4Gamepad:
                return "PS4 / Gamepad";
            case InputMode.PhoneMotion:
                return "Phone Motion";
            default:
                return "Auto";
        }
    }

    private static float NormalizeSignedAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    // Future controller-specific gyro data should still be merged here so CameraMover keeps consuming one clean tilt signal.
    private Vector2 ReadGyroTiltExtension()
    {
        return Vector2.zero;
    }
}
