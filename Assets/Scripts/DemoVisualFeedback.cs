using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DemoVisualFeedback : MonoBehaviour
{
    [Serializable]
    public struct VisualTuning
    {
        public Color surfaceBaseColor;
        public Color peakColor;
        public float idleBaselineIntensity;
        public float idlePulseIntensity;
        public float inputIntensityMultiplier;
        public float audioEnergyWeight;
        public float maxAudioContribution;
        public float intensitySmoothTime;
        public float simulatedBpm;
        public float rhythmPulseStrength;
        public float rhythmScaleStrength;
        public float rhythmBrightnessStrength;
        public float idleBreathSpeed;
        public float idleBreathScale;
        public float inputScaleStrength;
        public float colorBlendStrength;
        public float emissionStrength;
    }

    [SerializeField] private InputRouter inputRouter;
    [SerializeField] private Renderer targetRenderer;

    [Header("Intensity")]
    [SerializeField, Range(0f, 0.5f)] private float idleBaselineIntensity = 0.08f;
    [SerializeField, Range(0f, 1f)] private float idlePulseIntensity = 0.05f;
    [SerializeField, Min(0f)] private float inputIntensityMultiplier = 1f;
    [SerializeField, Min(0f)] private float audioEnergyWeight = 0.45f;
    [SerializeField, Range(0f, 1f)] private float maxAudioContribution = 0.2f;
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
    public float ExternalAudioEnergy => externalAudioEnergy;

    private Vector3 baseScale;
    private float smoothedMotion;
    private float motionVelocity;
    private float externalAudioEnergy;
    private readonly List<Material> runtimeMaterials = new List<Material>();
    private readonly List<Color> baseColors = new List<Color>();
    private Color baseColor = Color.white;
    private VisualTuning defaultTuning;
    private bool hasDefaultTuning;

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
        defaultTuning = CaptureCurrentTuning();
        hasDefaultTuning = true;
    }

    private void OnEnable()
    {
        baseScale = transform.localScale;
        smoothedMotion = 0f;
        motionVelocity = 0f;
        externalAudioEnergy = 0f;
        CurrentInteractionIntensity = idleBaselineIntensity;
        CurrentIntensity = idleBaselineIntensity;
        CurrentRhythmPulse = 0f;
        ApplyVisuals(CurrentIntensity, 0f, 0f, 0f);
    }

    private void Start()
    {
        baseScale = transform.localScale;
        CacheMaterialState();

        if (!hasDefaultTuning)
        {
            defaultTuning = CaptureCurrentTuning();
            hasDefaultTuning = true;
        }

        ApplyVisuals(CurrentIntensity, 0f, CurrentRhythmPulse, 0f);
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
        float idlePulseContribution = idleBaselineIntensity + idleWave * idlePulseIntensity;
        float motionContribution = smoothedMotion * inputIntensityMultiplier;
        float audioEnergyContribution = GetAudioEnergyContribution();
        CurrentInteractionIntensity = Mathf.Clamp01(idlePulseContribution + motionContribution + audioEnergyContribution);

        float beatsPerSecond = simulatedBpm / 60f;
        CurrentRhythmPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * beatsPerSecond * Mathf.PI * 2f);
        float rhythmContribution = CurrentRhythmPulse * rhythmPulseStrength;
        CurrentIntensity = Mathf.Clamp01(
            idlePulseContribution +
            rhythmContribution +
            motionContribution +
            audioEnergyContribution);

        float combinedReactiveEnergy = Mathf.Clamp01(motionContribution + audioEnergyContribution);
        ApplyVisuals(CurrentIntensity, idleWave, CurrentRhythmPulse, combinedReactiveEnergy);
    }

    public void SetInputRouter(InputRouter router)
    {
        inputRouter = router;
    }

    public void SetExternalAudioEnergy(float audioEnergy)
    {
        externalAudioEnergy = Mathf.Clamp01(audioEnergy);
    }

    public void ApplyRuntimeTuning(VisualTuning tuning)
    {
        baseColor = tuning.surfaceBaseColor;
        peakColor = tuning.peakColor;
        idleBaselineIntensity = Mathf.Clamp(tuning.idleBaselineIntensity, 0f, 0.5f);
        idlePulseIntensity = Mathf.Clamp01(tuning.idlePulseIntensity);
        inputIntensityMultiplier = Mathf.Max(0f, tuning.inputIntensityMultiplier);
        audioEnergyWeight = Mathf.Max(0f, tuning.audioEnergyWeight);
        maxAudioContribution = Mathf.Clamp01(tuning.maxAudioContribution);
        intensitySmoothTime = Mathf.Max(0.01f, tuning.intensitySmoothTime);
        simulatedBpm = Mathf.Max(1f, tuning.simulatedBpm);
        rhythmPulseStrength = Mathf.Clamp01(tuning.rhythmPulseStrength);
        rhythmScaleStrength = Mathf.Max(0f, tuning.rhythmScaleStrength);
        rhythmBrightnessStrength = Mathf.Clamp01(tuning.rhythmBrightnessStrength);
        idleBreathSpeed = Mathf.Max(0f, tuning.idleBreathSpeed);
        idleBreathScale = Mathf.Max(0f, tuning.idleBreathScale);
        inputScaleStrength = Mathf.Max(0f, tuning.inputScaleStrength);
        colorBlendStrength = Mathf.Clamp01(tuning.colorBlendStrength);
        emissionStrength = Mathf.Max(0f, tuning.emissionStrength);
        float combinedReactiveEnergy = Mathf.Clamp01(
            smoothedMotion * inputIntensityMultiplier +
            GetAudioEnergyContribution());
        ApplyVisuals(CurrentIntensity, 0f, CurrentRhythmPulse, combinedReactiveEnergy);
    }

    public void ResetRuntimeTuning()
    {
        if (!hasDefaultTuning)
        {
            return;
        }

        ApplyRuntimeTuning(defaultTuning);
    }

    private VisualTuning CaptureCurrentTuning()
    {
        return new VisualTuning
        {
            surfaceBaseColor = baseColor,
            peakColor = peakColor,
            idleBaselineIntensity = idleBaselineIntensity,
            idlePulseIntensity = idlePulseIntensity,
            inputIntensityMultiplier = inputIntensityMultiplier,
            audioEnergyWeight = audioEnergyWeight,
            maxAudioContribution = maxAudioContribution,
            intensitySmoothTime = intensitySmoothTime,
            simulatedBpm = simulatedBpm,
            rhythmPulseStrength = rhythmPulseStrength,
            rhythmScaleStrength = rhythmScaleStrength,
            rhythmBrightnessStrength = rhythmBrightnessStrength,
            idleBreathSpeed = idleBreathSpeed,
            idleBreathScale = idleBreathScale,
            inputScaleStrength = inputScaleStrength,
            colorBlendStrength = colorBlendStrength,
            emissionStrength = emissionStrength
        };
    }

    private void CacheMaterialState()
    {
        runtimeMaterials.Clear();
        baseColors.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (renderer.GetComponentInParent<DemoParticleController>() != null)
            {
                continue;
            }

            Material material = renderer.material;
            if (material == null)
            {
                continue;
            }

            runtimeMaterials.Add(material);

            Color rendererBaseColor = Color.white;
            if (material.HasProperty(BaseColorId))
            {
                rendererBaseColor = material.GetColor(BaseColorId);
            }
            else if (material.HasProperty(ColorId))
            {
                rendererBaseColor = material.GetColor(ColorId);
            }

            baseColors.Add(rendererBaseColor);
        }

        if (baseColors.Count > 0)
        {
            baseColor = baseColors[0];
        }
    }

    private void ApplyVisuals(float intensity, float idleWave, float rhythmPulse, float combinedReactiveEnergy)
    {
        float idleScaleOffset = Mathf.Lerp(-idleBreathScale, idleBreathScale, idleWave);
        float motionScaleOffset = combinedReactiveEnergy * inputScaleStrength;
        float rhythmScaleOffset = rhythmPulse * rhythmScaleStrength * Mathf.Lerp(0.4f, 1f, CurrentInteractionIntensity);
        float scaleMultiplier = 1f + idleScaleOffset + motionScaleOffset + rhythmScaleOffset;
        transform.localScale = baseScale * scaleMultiplier;

        if (runtimeMaterials.Count == 0)
        {
            return;
        }

        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            Material material = runtimeMaterials[i];
            if (material == null)
            {
                continue;
            }

            Color sourceColor = baseColor;
            if (i < baseColors.Count)
            {
                sourceColor = Color.Lerp(baseColors[i], baseColor, 0.45f);
            }

            Color surfaceColor = Color.Lerp(sourceColor, peakColor, intensity * colorBlendStrength);
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, surfaceColor);
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, surfaceColor);
            }

            if (material.HasProperty(EmissionColorId))
            {
                material.EnableKeyword("_EMISSION");
                float rhythmicBrightness = intensity + rhythmPulse * rhythmBrightnessStrength + GetAudioEnergyContribution();
                material.SetColor(EmissionColorId, surfaceColor * (rhythmicBrightness * emissionStrength));
            }
        }
    }

    private float GetAudioEnergyContribution()
    {
        return Mathf.Min(externalAudioEnergy * audioEnergyWeight, maxAudioContribution);
    }
}
