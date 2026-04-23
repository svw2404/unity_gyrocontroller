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
    [SerializeField, Min(1f)] private float phonePoseSharpness = 10f;

    private Vector2 smoothedInput;
    private Vector2 inputVelocity;
    private Quaternion accumulatedRotation;
    private Quaternion phoneNeutralRotation;
    private bool wasUsingPhoneMotion;

    private void Reset()
    {
        inputRouter = FindAnyObjectByType<InputRouter>();
        enablePitchInput = true;
        clampPitch = false;
    }

    private void Awake()
    {
        // Older serialized scene instances may still have this off from a previous demo version.
        enablePitchInput = true;
        clampPitch = false;

        if (inputRouter == null)
        {
            inputRouter = FindAnyObjectByType<InputRouter>();
        }

        SyncRotationFromTransform();
    }

    private void OnEnable()
    {
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
            }

            Quaternion targetRotation = phoneNeutralRotation *
                Quaternion.Euler(
                    -blendedInput.y * phonePitchRange,
                    blendedInput.x * phoneYawRange,
                    0f);

            float blend = 1f - Mathf.Exp(-phonePoseSharpness * Time.deltaTime);
            accumulatedRotation = Quaternion.Slerp(accumulatedRotation, targetRotation, blend);
            transform.localRotation = accumulatedRotation;
            wasUsingPhoneMotion = true;
            return;
        }

        if (wasUsingPhoneMotion)
        {
            accumulatedRotation = transform.localRotation;
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
        wasUsingPhoneMotion = false;
    }

    private void SyncRotationFromTransform()
    {
        accumulatedRotation = transform.localRotation;
        phoneNeutralRotation = transform.localRotation;
    }
}
