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

    private Vector2 smoothedInput;
    private Vector2 inputVelocity;
    private Quaternion accumulatedRotation;

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
    }

    private void SyncRotationFromTransform()
    {
        accumulatedRotation = transform.localRotation;
    }
}
