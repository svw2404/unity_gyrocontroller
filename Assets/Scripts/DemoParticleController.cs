using System;
using UnityEngine;

[DisallowMultipleComponent]
public class DemoParticleController : MonoBehaviour
{
    [Serializable]
    public struct ParticleTuning
    {
        public int maxParticles;
        public int orbitBallCount;
        public float spawnRadius;
        public float orbitSpeed;
        public float orbitTurbulenceStrength;
        public float orbitBallScale;
        public float particleLifetime;
        public float particleSize;
        public float idleStartSpeed;
        public float idleEmissionRate;
        public float idleNoiseStrength;
        public float responseSmoothTime;
        public float speedBoost;
        public float emissionBoost;
        public float noiseBoost;
        public float rhythmExpansionStrength;
        public float audioExpansionStrength;
        public float motionExpansionStrength;
        public float brightnessStrength;
        public float movementSpeedMultiplier;
        public float maxExpansion;
        public Color idleColor;
        public Color activeColor;
    }

    [SerializeField] private InputRouter inputRouter;
    [SerializeField] private DemoVisualFeedback visualFeedback;
    [SerializeField] private ParticleSystem targetParticleSystem;
    [SerializeField] private Transform particleCenter;
    [SerializeField] private Transform orbitBallParent;
    [SerializeField] private Transform[] assignedOrbitBalls;

    [Header("Spawn")]
    [SerializeField, Min(1)] private int maxParticles = 32;
    [SerializeField, Min(1)] private int orbitBallCount = 8;
    [SerializeField, Min(0.1f)] private float spawnRadius = 1.4f;
    [SerializeField, Min(0f)] private float orbitSpeed = 0.22f;
    [SerializeField, Min(0f)] private float orbitTurbulenceStrength = 0.04f;
    [SerializeField, Min(0.01f)] private float orbitBallScale = 0.16f;
    [SerializeField, Min(0.1f)] private float particleLifetime = 5.5f;
    [SerializeField, Min(0.01f)] private float particleSize = 0.12f;

    [Header("Idle Motion")]
    [SerializeField, Min(0f)] private float idleStartSpeed = 0.10f;
    [SerializeField, Min(0f)] private float idleEmissionRate = 10f;
    [SerializeField, Min(0f)] private float idleNoiseStrength = 0.12f;
    [SerializeField, Min(0.01f)] private float responseSmoothTime = 0.18f;

    [Header("Intensity Response")]
    [SerializeField, Min(0f)] private float speedBoost = 0.16f;
    [SerializeField, Min(0f)] private float emissionBoost = 6f;
    [SerializeField, Min(0f)] private float noiseBoost = 0.14f;

    [Header("Radial Breathing")]
    [SerializeField, Min(0f)] private float rhythmExpansionStrength = 0.18f;
    [SerializeField, Min(0f)] private float audioExpansionStrength = 0.08f;
    [SerializeField, Min(0f)] private float motionExpansionStrength = 0.10f;
    [SerializeField, Min(0f)] private float brightnessStrength = 0.55f;
    [SerializeField, Min(0f)] private float movementSpeedMultiplier = 1f;
    [SerializeField, Min(0f)] private float maxExpansion = 0.45f;

    [SerializeField] private Color idleColor = new Color(1f, 0.83f, 0.87f, 0.30f);
    [SerializeField] private Color activeColor = new Color(1f, 0.97f, 0.99f, 0.78f);

    public float CurrentParticleIntensity { get; private set; }
    public float CurrentParticleRadius { get; private set; }
    public float CurrentParticlePulse { get; private set; }
    public float CurrentOrbitSpeed { get; private set; }

    private float intensityVelocity;
    private float radiusVelocity;
    private Material runtimeMaterial;
    private ParticleTuning defaultTuning;
    private bool hasDefaultTuning;
    private Transform orbitBallRoot;
    private readonly System.Collections.Generic.List<Transform> orbitBallTransforms = new System.Collections.Generic.List<Transform>();
    private readonly System.Collections.Generic.List<Renderer> orbitBallRenderers = new System.Collections.Generic.List<Renderer>();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Reset()
    {
        inputRouter = FindAnyObjectByType<InputRouter>();
        visualFeedback = GetComponentInParent<DemoVisualFeedback>();
        targetParticleSystem = GetComponent<ParticleSystem>();
        if (particleCenter == null)
        {
            particleCenter = transform.parent != null ? transform.parent : transform;
        }
    }

    private void Awake()
    {
        if (inputRouter == null)
        {
            inputRouter = FindAnyObjectByType<InputRouter>();
        }

        if (visualFeedback == null)
        {
            visualFeedback = GetComponentInParent<DemoVisualFeedback>();
        }

        if (targetParticleSystem == null)
        {
            targetParticleSystem = GetComponent<ParticleSystem>();
        }

        if (particleCenter == null)
        {
            particleCenter = transform.parent != null ? transform.parent : transform;
        }

        EnsureOrbitBallRoot();
        ConfigureParticleSystem();
        defaultTuning = CaptureCurrentTuning();
        hasDefaultTuning = true;
    }

    private void OnEnable()
    {
        intensityVelocity = 0f;
        radiusVelocity = 0f;
        CurrentParticleIntensity = 0f;
        CurrentParticleRadius = spawnRadius;
        CurrentParticlePulse = 0f;
        CurrentOrbitSpeed = orbitSpeed;
        ConfigureParticleSystem();

        if (targetParticleSystem != null && !targetParticleSystem.isPlaying)
        {
            targetParticleSystem.Play();
        }
    }

    private void Update()
    {
        if (targetParticleSystem == null)
        {
            return;
        }

        UpdateParticleCenterTransform();

        float rhythmPulse = visualFeedback != null ? visualFeedback.CurrentRhythmPulse : 0f;
        float audioEnergy = visualFeedback != null ? visualFeedback.ExternalAudioEnergy : 0f;
        float motionIntensity = inputRouter != null
            ? inputRouter.MotionMagnitude
            : visualFeedback != null ? visualFeedback.CurrentInteractionIntensity : 0f;

        float rhythmContribution = rhythmPulse * rhythmExpansionStrength;
        float audioContribution = audioEnergy * audioExpansionStrength;
        float motionContribution = motionIntensity * motionExpansionStrength;
        float totalContribution = rhythmContribution + audioContribution + motionContribution;

        float targetIntensity = Mathf.Clamp01(totalContribution);
        float targetRadius = spawnRadius + Mathf.Min(maxExpansion, totalContribution);

        CurrentParticleIntensity = Mathf.SmoothDamp(
            CurrentParticleIntensity,
            targetIntensity,
            ref intensityVelocity,
            responseSmoothTime);

        CurrentParticleRadius = Mathf.SmoothDamp(
            CurrentParticleRadius,
            targetRadius,
            ref radiusVelocity,
            responseSmoothTime);

        CurrentParticlePulse = Mathf.InverseLerp(
            spawnRadius,
            spawnRadius + Mathf.Max(0.001f, maxExpansion),
            CurrentParticleRadius);

        float orbitEnergy = Mathf.Clamp01(
            rhythmPulse * 0.50f +
            audioEnergy * 0.32f +
            motionIntensity * 0.18f);
        float orbitSpeedMultiplier = 1f + orbitEnergy * Mathf.Lerp(0.12f, 0.42f, Mathf.Clamp01(movementSpeedMultiplier));
        CurrentOrbitSpeed = Mathf.Clamp(
            orbitSpeed * orbitSpeedMultiplier,
            0.01f,
            orbitSpeed * 1.55f);
        ApplyIntensity(CurrentParticleIntensity, CurrentParticlePulse);
        UpdateOrbitBalls(CurrentParticleIntensity, CurrentParticlePulse, audioEnergy, motionIntensity);
    }

    public void SetInputRouter(InputRouter router)
    {
        inputRouter = router;
    }

    public void SetVisualFeedback(DemoVisualFeedback feedback)
    {
        visualFeedback = feedback;
    }

    public void SetParticleCenter(Transform center)
    {
        particleCenter = center;
        UpdateParticleCenterTransform();
    }

    public void SetOrbitBallParent(Transform parent)
    {
        orbitBallParent = parent;
        Transform desiredParent = orbitBallParent != null ? orbitBallParent : transform;

        if (orbitBallRoot != null && !HasAssignedOrbitBalls())
        {
            orbitBallRoot.SetParent(desiredParent, false);
            return;
        }

        orbitBallRoot = null;
        orbitBallTransforms.Clear();
        orbitBallRenderers.Clear();
        EnsureOrbitBallRoot();
        RebuildOrbitBalls();
    }

    public void ApplyRuntimeTuning(ParticleTuning tuning)
    {
        maxParticles = Mathf.Max(1, tuning.maxParticles);
        orbitBallCount = Mathf.Max(1, tuning.orbitBallCount);
        spawnRadius = Mathf.Max(0.1f, tuning.spawnRadius);
        orbitSpeed = Mathf.Max(0f, tuning.orbitSpeed);
        orbitTurbulenceStrength = Mathf.Max(0f, tuning.orbitTurbulenceStrength);
        orbitBallScale = Mathf.Max(0.01f, tuning.orbitBallScale);
        particleLifetime = Mathf.Max(0.1f, tuning.particleLifetime);
        particleSize = Mathf.Max(0.01f, tuning.particleSize);
        idleStartSpeed = Mathf.Max(0f, tuning.idleStartSpeed);
        idleEmissionRate = Mathf.Max(0f, tuning.idleEmissionRate);
        idleNoiseStrength = Mathf.Max(0f, tuning.idleNoiseStrength);
        responseSmoothTime = Mathf.Max(0.01f, tuning.responseSmoothTime);
        speedBoost = Mathf.Max(0f, tuning.speedBoost);
        emissionBoost = Mathf.Max(0f, tuning.emissionBoost);
        noiseBoost = Mathf.Max(0f, tuning.noiseBoost);
        rhythmExpansionStrength = Mathf.Max(0f, tuning.rhythmExpansionStrength);
        audioExpansionStrength = Mathf.Max(0f, tuning.audioExpansionStrength);
        motionExpansionStrength = Mathf.Max(0f, tuning.motionExpansionStrength);
        brightnessStrength = Mathf.Max(0f, tuning.brightnessStrength);
        movementSpeedMultiplier = Mathf.Max(0f, tuning.movementSpeedMultiplier);
        maxExpansion = Mathf.Max(0f, tuning.maxExpansion);
        idleColor = tuning.idleColor;
        activeColor = tuning.activeColor;
        ConfigureParticleSystem();
    }

    public void ResetRuntimeTuning()
    {
        if (!hasDefaultTuning)
        {
            return;
        }

        ApplyRuntimeTuning(defaultTuning);
    }

    private ParticleTuning CaptureCurrentTuning()
    {
        return new ParticleTuning
        {
            maxParticles = maxParticles,
            orbitBallCount = orbitBallCount,
            spawnRadius = spawnRadius,
            orbitSpeed = orbitSpeed,
            orbitTurbulenceStrength = orbitTurbulenceStrength,
            orbitBallScale = orbitBallScale,
            particleLifetime = particleLifetime,
            particleSize = particleSize,
            idleStartSpeed = idleStartSpeed,
            idleEmissionRate = idleEmissionRate,
            idleNoiseStrength = idleNoiseStrength,
            responseSmoothTime = responseSmoothTime,
            speedBoost = speedBoost,
            emissionBoost = emissionBoost,
            noiseBoost = noiseBoost,
            rhythmExpansionStrength = rhythmExpansionStrength,
            audioExpansionStrength = audioExpansionStrength,
            motionExpansionStrength = motionExpansionStrength,
            brightnessStrength = brightnessStrength,
            movementSpeedMultiplier = movementSpeedMultiplier,
            maxExpansion = maxExpansion,
            idleColor = idleColor,
            activeColor = activeColor
        };
    }

    private void ConfigureParticleSystem()
    {
        if (targetParticleSystem == null)
        {
            return;
        }

        UpdateParticleCenterTransform();
        EnsureOrbitBallRoot();
        RebuildOrbitBalls();

        ParticleSystem.MainModule main = targetParticleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 8f;
        // Keep particles local so the whole cloud can breathe around the object as one group.
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = maxParticles;
        main.startLifetime = particleLifetime;
        main.startSize = particleSize;
        main.startSpeed = idleStartSpeed;
        main.startColor = idleColor;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = targetParticleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = idleEmissionRate;

        ParticleSystem.ShapeModule shape = targetParticleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = spawnRadius;
        shape.radiusThickness = 1f;

        ParticleSystem.NoiseModule noise = targetParticleSystem.noise;
        noise.enabled = true;
        noise.separateAxes = false;
        noise.strength = idleNoiseStrength;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystemRenderer particleRenderer = targetParticleSystem.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            EnsureParticleMaterial(particleRenderer);
        }

        targetParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        targetParticleSystem.Play();
        ApplyIntensity(CurrentParticleIntensity, CurrentParticlePulse);
        UpdateOrbitBalls(CurrentParticleIntensity, CurrentParticlePulse, 0f, 0f);
    }

    private void EnsureParticleMaterial(ParticleSystemRenderer particleRenderer)
    {
        if (runtimeMaterial != null)
        {
            particleRenderer.material = runtimeMaterial;
            SetMaterialColor(idleColor);
            return;
        }

        if (particleRenderer.sharedMaterial != null)
        {
            runtimeMaterial = new Material(particleRenderer.sharedMaterial);
            particleRenderer.material = runtimeMaterial;
            SetMaterialColor(idleColor);
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        }

        if (shader == null)
        {
            return;
        }

        runtimeMaterial = new Material(shader);
        particleRenderer.material = runtimeMaterial;
        SetMaterialColor(idleColor);
    }

    private void ApplyIntensity(float intensity, float pulse)
    {
        if (targetParticleSystem == null)
        {
            return;
        }

        ParticleSystem.MainModule main = targetParticleSystem.main;
        float movementResponse = Mathf.Clamp01((intensity * 0.75f + pulse * 0.25f) * movementSpeedMultiplier);
        float brightnessResponse = Mathf.Clamp01(intensity + pulse * brightnessStrength * 0.35f);
        Color particleColor = Color.Lerp(idleColor, activeColor, brightnessResponse);
        particleColor = Color.Lerp(
            particleColor,
            Color.white,
            Mathf.Clamp01(brightnessResponse * brightnessStrength * 0.35f));
        particleColor.a = Mathf.Lerp(idleColor.a, activeColor.a, Mathf.Clamp01(brightnessResponse + pulse * 0.15f));

        main.startSize = particleSize * (1f + pulse * 0.18f);
        main.startSpeed = idleStartSpeed + movementResponse * speedBoost + pulse * speedBoost * 0.12f;
        main.startColor = particleColor;

        ParticleSystem.EmissionModule emission = targetParticleSystem.emission;
        emission.rateOverTime = idleEmissionRate + movementResponse * emissionBoost + pulse * emissionBoost * 0.18f;

        ParticleSystem.ShapeModule shape = targetParticleSystem.shape;
        shape.radius = CurrentParticleRadius > 0f ? CurrentParticleRadius : spawnRadius;

        ParticleSystem.NoiseModule noise = targetParticleSystem.noise;
        noise.strength = idleNoiseStrength + movementResponse * noiseBoost;
        noise.scrollSpeed = 0.15f + movementResponse * 0.14f + pulse * 0.04f;

        SetMaterialColor(particleColor);
    }

    private void EnsureOrbitBallRoot()
    {
        if (orbitBallRoot != null)
        {
            return;
        }

        Transform rootParent = orbitBallParent != null ? orbitBallParent : transform;
        Transform existing = rootParent.Find("Generated Rhythm Balls");
        if (existing != null)
        {
            orbitBallRoot = existing;
            CacheOrbitBallLists();
            return;
        }

        GameObject rootObject = new GameObject("Generated Rhythm Balls");
        rootObject.transform.SetParent(rootParent, false);
        orbitBallRoot = rootObject.transform;
    }

    private void RebuildOrbitBalls()
    {
        orbitBallTransforms.Clear();
        orbitBallRenderers.Clear();

        if (HasAssignedOrbitBalls())
        {
            if (orbitBallRoot != null)
            {
                orbitBallRoot.gameObject.SetActive(false);
            }

            CacheAssignedOrbitBallLists();
            return;
        }

        if (orbitBallRoot == null)
        {
            return;
        }

        orbitBallRoot.gameObject.SetActive(true);

        if (orbitBallRoot.childCount == orbitBallCount && orbitBallTransforms.Count == orbitBallCount)
        {
            return;
        }

        for (int i = orbitBallRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(orbitBallRoot.GetChild(i).gameObject);
        }

        for (int i = 0; i < orbitBallCount; i++)
        {
            GameObject orbitBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orbitBall.name = $"Orbit Ball {i + 1}";
            orbitBall.layer = gameObject.layer;
            orbitBall.transform.SetParent(orbitBallRoot, false);

            Collider collider = orbitBall.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            orbitBallTransforms.Add(orbitBall.transform);
            orbitBallRenderers.Add(orbitBall.GetComponent<Renderer>());
        }
    }

    private void CacheOrbitBallLists()
    {
        orbitBallTransforms.Clear();
        orbitBallRenderers.Clear();

        if (orbitBallRoot == null)
        {
            return;
        }

        for (int i = 0; i < orbitBallRoot.childCount; i++)
        {
            Transform child = orbitBallRoot.GetChild(i);
            orbitBallTransforms.Add(child);
            orbitBallRenderers.Add(child.GetComponent<Renderer>());
        }
    }

    private void CacheAssignedOrbitBallLists()
    {
        for (int i = 0; i < assignedOrbitBalls.Length; i++)
        {
            Transform assigned = assignedOrbitBalls[i];
            if (assigned == null)
            {
                continue;
            }

            orbitBallTransforms.Add(assigned);
            orbitBallRenderers.Add(assigned.GetComponent<Renderer>());
        }
    }

    private void UpdateOrbitBalls(float intensity, float pulse, float audioEnergy, float motionIntensity)
    {
        if (orbitBallTransforms.Count == 0)
        {
            return;
        }

        if (orbitBallRoot == null && !HasAssignedOrbitBalls())
        {
            return;
        }

        float angleStep = 360f / orbitBallTransforms.Count;
        float orbitAngle = Time.time * CurrentOrbitSpeed * 60f;
        float verticalAmplitude = 0.03f + pulse * 0.06f + motionIntensity * 0.03f;
        float turbulence = orbitTurbulenceStrength * Mathf.Lerp(0.35f, 1f, pulse);
        float brightness = Mathf.Clamp01(intensity + audioEnergy * brightnessStrength * 0.25f);
        Vector3 center = particleCenter != null ? particleCenter.position : transform.position;

        for (int i = 0; i < orbitBallTransforms.Count; i++)
        {
            Transform orbitBall = orbitBallTransforms[i];
            Renderer orbitRenderer = orbitBallRenderers[i];
            float angle = orbitAngle + angleStep * i;
            float radians = angle * Mathf.Deg2Rad;
            Vector3 baseDirection = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));

            float structuredOffset =
                Mathf.Sin(Time.time * (CurrentOrbitSpeed * 1.8f + 0.35f) + i * 0.92f) * turbulence +
                Mathf.Cos(Time.time * 0.85f + i * 1.17f) * turbulence * 0.5f;

            float localRadius = Mathf.Clamp(
                CurrentParticleRadius + structuredOffset,
                spawnRadius * 0.6f,
                spawnRadius + maxExpansion);

            float verticalOffset = Mathf.Sin(Time.time * (CurrentOrbitSpeed * 0.9f + 0.2f) + i * 1.27f) * verticalAmplitude;
            Vector3 targetPosition = center + baseDirection * localRadius + Vector3.up * verticalOffset;
            orbitBall.position = Vector3.Lerp(
                orbitBall.position,
                targetPosition,
                1f - Mathf.Exp(-8f * Time.deltaTime));

            float ballScale = orbitBallScale * (1f + pulse * 0.28f + intensity * 0.12f);
            orbitBall.localScale = Vector3.one * ballScale;

            if (orbitRenderer != null)
            {
                Color orbitColor = Color.Lerp(idleColor, activeColor, brightness);
                orbitColor = Color.Lerp(orbitColor, Color.white, Mathf.Clamp01(pulse * brightnessStrength * 0.3f));
                ApplyRendererColor(orbitRenderer, orbitColor);
            }
        }
    }

    private bool HasAssignedOrbitBalls()
    {
        if (assignedOrbitBalls == null)
        {
            return false;
        }

        for (int i = 0; i < assignedOrbitBalls.Length; i++)
        {
            if (assignedOrbitBalls[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateParticleCenterTransform()
    {
        if (targetParticleSystem == null || particleCenter == null)
        {
            return;
        }

        targetParticleSystem.transform.position = particleCenter.position;
    }

    private void SetMaterialColor(Color color)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (runtimeMaterial.HasProperty(BaseColorId))
        {
            runtimeMaterial.SetColor(BaseColorId, color);
        }

        if (runtimeMaterial.HasProperty(ColorId))
        {
            runtimeMaterial.SetColor(ColorId, color);
        }
    }

    private void ApplyRendererColor(Renderer renderer, Color color)
    {
        Material material = renderer.material;
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, color);
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, color);
        }
    }
}
