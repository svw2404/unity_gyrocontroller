using UnityEngine;

[DisallowMultipleComponent]
public class ObjectRotator : MonoBehaviour
{
    [SerializeField] private InputRouter inputRouter;

    [Header("Rotation Speed")]
    [SerializeField] private float yawSpeed = 120f;
    [SerializeField] private float pitchSpeed = 140f;

    [Header("Smoothing")]
    [SerializeField, Min(0.01f)] private float inputSmoothTime = 0.12f;

    [Header("Pitch (Optional)")]
    [SerializeField] private bool enablePitchInput = true;
    [SerializeField] private bool clampPitch = false;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-80f, 80f);

    [Header("Phone Motion")]
    [SerializeField] private bool phoneUsesDirectPose = true;
    [SerializeField] private float phoneYawRange = 80f;
    [SerializeField] private float phonePitchRange = 65f;
    [SerializeField] private float phoneHeightRange = 1f;
    [SerializeField, Range(0f, 1f)] private float phoneLiftRotationLockThreshold = 0.45f;
    [SerializeField, Range(0f, 1f)] private float phoneLiftRotationReleaseThreshold = 0.25f;
    [SerializeField, Min(1f)] private float phonePoseSharpness = 10f;

    private Vector2 smoothedInput;
    private Vector2 inputVelocity;
    private Quaternion accumulatedRotation;
    private Quaternion phoneNeutralRotation;
    private Vector3 phoneNeutralPosition;
    private bool phoneLiftRotationLockActive;
    private bool wasUsingPhoneMotion;

    private void Reset()
    {
        inputRouter = FindAnyObjectByType<InputRouter>();
        enablePitchInput = true;
        clampPitch = false;
        phoneUsesDirectPose = true;
        EnsurePhoneMotionDefaults();
    }

    private void Awake()
    {
        // Older serialized scene instances may still have this off from a previous demo version.
        enablePitchInput = true;
        clampPitch = false;
        phoneUsesDirectPose = true;
        EnsurePhoneMotionDefaults();

        if (inputRouter == null)
        {
            inputRouter = FindAnyObjectByType<InputRouter>();
        }

        SyncRotationFromTransform();
    }

    private void OnEnable()
    {
        EnsurePhoneMotionDefaults();
        smoothedInput = Vector2.zero;
        inputVelocity = Vector2.zero;
        SyncRotationFromTransform();
    }

    private void Update()
    {
        if (inputRouter == null)
        {
            return;
        }

        Vector2 targetInput = Vector2.ClampMagnitude(inputRouter.ObjectRotation, 1f);
        smoothedInput = Vector2.SmoothDamp(
            smoothedInput,
            targetInput,
            ref inputVelocity,
            inputSmoothTime);

        Vector2 blendedInput = Vector2.ClampMagnitude(smoothedInput, 1f);
        if (!enablePitchInput)
        {
            blendedInput.y = 0f;
        }

        bool usingPhoneMotion = inputRouter.IsUsingPhoneMotion && phoneUsesDirectPose;
        if (usingPhoneMotion)
        {
            if (!wasUsingPhoneMotion)
            {
                phoneNeutralRotation = transform.localRotation;
                accumulatedRotation = transform.localRotation;
                phoneLiftRotationLockActive = false;
            }

            UpdatePhoneLiftRotationLock(blendedInput, Mathf.Abs(inputRouter.PhoneObjectLift));

            Vector3 targetPosition = phoneNeutralPosition + Vector3.up * (inputRouter.PhoneObjectLift * phoneHeightRange);
            float blend = 1f - Mathf.Exp(-phonePoseSharpness * Time.deltaTime);

            if (!phoneLiftRotationLockActive)
            {
                Quaternion targetRotation = phoneNeutralRotation *
                    Quaternion.Euler(
                        -blendedInput.y * phonePitchRange,
                        blendedInput.x * phoneYawRange,
                        0f);
                accumulatedRotation = Quaternion.Slerp(accumulatedRotation, targetRotation, blend);
            }

            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, blend);
            transform.localRotation = accumulatedRotation;
            wasUsingPhoneMotion = true;
            return;
        }

        if (wasUsingPhoneMotion)
        {
            transform.localPosition = phoneNeutralPosition;
            accumulatedRotation = transform.localRotation;
            phoneLiftRotationLockActive = false;
            wasUsingPhoneMotion = false;
        }

        float yawDelta = blendedInput.x * yawSpeed * Time.deltaTime;
        float pitchDelta = -blendedInput.y * pitchSpeed * Time.deltaTime;

        // Treat the 2D rotation input as one blended motion so diagonals feel deliberate, not over-speed.
        accumulatedRotation = Quaternion.AngleAxis(yawDelta, Vector3.up) * accumulatedRotation;
        accumulatedRotation *= Quaternion.AngleAxis(pitchDelta, Vector3.right);
        transform.localRotation = accumulatedRotation;
    }

    public void SetInputRouter(InputRouter router)
    {
        inputRouter = router;
    }

    public void SnapToCurrentTransform()
    {
        SyncRotationFromTransform();
        smoothedInput = Vector2.zero;
        inputVelocity = Vector2.zero;
        phoneLiftRotationLockActive = false;
        wasUsingPhoneMotion = false;
    }

    private void SyncRotationFromTransform()
    {
        accumulatedRotation = transform.localRotation;
        phoneNeutralRotation = transform.localRotation;
        phoneNeutralPosition = transform.localPosition;
    }

    private void EnsurePhoneMotionDefaults()
    {
        if (phoneYawRange <= 0f)
        {
            phoneYawRange = 80f;
        }

        if (phonePitchRange <= 0f)
        {
            phonePitchRange = 65f;
        }

        if (Mathf.Approximately(phoneHeightRange, 0f))
        {
            phoneHeightRange = 1f;
        }

        if (phonePoseSharpness < 1f)
        {
            phonePoseSharpness = 10f;
        }

        if (phoneLiftRotationLockThreshold <= 0f)
        {
            phoneLiftRotationLockThreshold = 0.45f;
        }

        if (phoneLiftRotationReleaseThreshold <= 0f)
        {
            phoneLiftRotationReleaseThreshold = 0.25f;
        }

        if (phoneLiftRotationReleaseThreshold >= phoneLiftRotationLockThreshold)
        {
            phoneLiftRotationReleaseThreshold = Mathf.Max(0.05f, phoneLiftRotationLockThreshold - 0.1f);
        }
    }

    // When the performer clearly rolls the phone to lift the cube, freeze rotation at the current pose.
    private void UpdatePhoneLiftRotationLock(Vector2 blendedInput, float liftMagnitude)
    {
        if (!phoneLiftRotationLockActive)
        {
            if (liftMagnitude >= phoneLiftRotationLockThreshold)
            {
                accumulatedRotation = transform.localRotation;
                phoneLiftRotationLockActive = true;
            }

            return;
        }

        if (liftMagnitude > phoneLiftRotationReleaseThreshold)
        {
            return;
        }

        Quaternion currentPhonePose = Quaternion.Euler(
            -blendedInput.y * phonePitchRange,
            blendedInput.x * phoneYawRange,
            0f);
        phoneNeutralRotation = transform.localRotation * Quaternion.Inverse(currentPhonePose);
        accumulatedRotation = transform.localRotation;
        phoneLiftRotationLockActive = false;
    }
}
