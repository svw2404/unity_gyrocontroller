using UnityEngine;

[DisallowMultipleComponent]
public class DemoVisualFeedback : MonoBehaviour
{
    [SerializeField] private InputRouter inputRouter;
    [SerializeField] private Renderer targetRenderer;

    [Header("Intensity")]
    [SerializeField, Range(0f, 0.5f)] private float idleBaselineIntensity = 0.08f;
    [SerializeField, Range(0f, 1f)] private float idlePulseIntensity = 0.05f;
    [SerializeField, Min(0f)] private float inputIntensityMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float intensitySmoothTime = 0.12f;

    [Header("Rhythm Simulation")]
    [SerializeField, Min(1f)] private float simulatedBpm = 76f;
    [SerializeField, Range(0f, 1f)] private float rhythmPulseStrength = 0.16f;
    [SerializeField, Min(0f)] private float rhythmScaleStrength = 0.035f;
    [SerializeField, Range(0f, 1f)] private float rhythmBrightnessStrength = 0.18f;

    [Header("Scale")]
    [SerializeField] private float idleBreathSpeed = 0.45f;
    [SerializeField, Min(0f)] private float idleBreathScale = 0.025f;
    [SerializeField, Min(0f)] private float inputScaleStrength = 0.14f;

    [Header("Color")]
    [SerializeField] private Color peakColor = new Color(1f, 0.92f, 0.92f, 1f);
    [SerializeField, Range(0f, 1f)] private float colorBlendStrength = 0.75f;
    [SerializeField, Min(0f)] private float emissionStrength = 0.8f;

    public float CurrentIntensity { get; private set; }
    public float CurrentInteractionIntensity { get; private set; }
    public float CurrentRhythmPulse { get; private set; }

    private Vector3 baseScale;
    private float smoothedMotion;
    private float motionVelocity;
    private Material runtimeMaterial;
    private Color baseColor = Color.white;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Reset()
    {
        inputRouter = FindAnyObjectByType<InputRouter>();
        targetRenderer = GetComponentInChildren<Renderer>();
    }

    private void Awake()
    {
        if (inputRouter == null)
        {
            inputRouter = FindAnyObjectByType<InputRouter>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        baseScale = transform.localScale;
        CacheMaterialState();
    }

    private void OnEnable()
    {
        baseScale = transform.localScale;
        smoothedMotion = 0f;
        motionVelocity = 0f;
        CurrentInteractionIntensity = idleBaselineIntensity;
        CurrentIntensity = idleBaselineIntensity;
        CurrentRhythmPulse = 0f;
        ApplyVisuals(CurrentIntensity, 0f, 0f);
    }

    private void Start()
    {
        baseScale = transform.localScale;
        CacheMaterialState();
        ApplyVisuals(CurrentIntensity, 0f, CurrentRhythmPulse);
    }

    private void Update()
    {
        float rawMotionMagnitude = inputRouter != null ? inputRouter.MotionMagnitude : 0f;
        smoothedMotion = Mathf.SmoothDamp(
            smoothedMotion,
            rawMotionMagnitude,
            ref motionVelocity,
            intensitySmoothTime);

        float idleWave = 0.5f + 0.5f * Mathf.Sin(Time.time * idleBreathSpeed * Mathf.PI * 2f);
        float idleIntensity = idleBaselineIntensity + idleWave * idlePulseIntensity;
        float motionIntensity = smoothedMotion * inputIntensityMultiplier;
        CurrentInteractionIntensity = Mathf.Clamp01(idleIntensity + motionIntensity);

        float beatsPerSecond = simulatedBpm / 60f;
        CurrentRhythmPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * beatsPerSecond * Mathf.PI * 2f);

        // Make the simulated beat more noticeable when the performer is interacting, while still present at idle.
        float rhythmCarrier = Mathf.Lerp(0.35f, 1f, smoothedMotion);
        float rhythmContribution = CurrentRhythmPulse * rhythmPulseStrength * rhythmCarrier;
        CurrentIntensity = Mathf.Clamp01(CurrentInteractionIntensity + rhythmContribution);

        ApplyVisuals(CurrentIntensity, idleWave, CurrentRhythmPulse);
    }

    public void SetInputRouter(InputRouter router)
    {
        inputRouter = router;
    }

    private void CacheMaterialState()
    {
        if (targetRenderer == null)
        {
            return;
        }

        runtimeMaterial = targetRenderer.material;
        if (runtimeMaterial == null)
        {
            return;
        }

        if (runtimeMaterial.HasProperty(BaseColorId))
        {
            baseColor = runtimeMaterial.GetColor(BaseColorId);
        }
        else if (runtimeMaterial.HasProperty(ColorId))
        {
            baseColor = runtimeMaterial.GetColor(ColorId);
        }
    }

    private void ApplyVisuals(float intensity, float idleWave, float rhythmPulse)
    {
        float idleScaleOffset = Mathf.Lerp(-idleBreathScale, idleBreathScale, idleWave);
        float motionScaleOffset = smoothedMotion * inputScaleStrength;
        float rhythmScaleOffset = rhythmPulse * rhythmScaleStrength * Mathf.Lerp(0.4f, 1f, CurrentInteractionIntensity);
        float scaleMultiplier = 1f + idleScaleOffset + motionScaleOffset + rhythmScaleOffset;
        transform.localScale = baseScale * scaleMultiplier;

        if (runtimeMaterial == null)
        {
            return;
        }

        Color surfaceColor = Color.Lerp(baseColor, peakColor, intensity * colorBlendStrength);
        if (runtimeMaterial.HasProperty(BaseColorId))
        {
            runtimeMaterial.SetColor(BaseColorId, surfaceColor);
        }

        if (runtimeMaterial.HasProperty(ColorId))
        {
            runtimeMaterial.SetColor(ColorId, surfaceColor);
        }

        if (runtimeMaterial.HasProperty(EmissionColorId))
        {
            runtimeMaterial.EnableKeyword("_EMISSION");
            float rhythmicBrightness = intensity + rhythmPulse * rhythmBrightnessStrength;
            runtimeMaterial.SetColor(EmissionColorId, surfaceColor * (rhythmicBrightness * emissionStrength));
        }
    }
}
