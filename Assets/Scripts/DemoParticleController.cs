using UnityEngine;

[DisallowMultipleComponent]
public class DemoParticleController : MonoBehaviour
{
    [SerializeField] private InputRouter inputRouter;
    [SerializeField] private DemoVisualFeedback visualFeedback;
    [SerializeField] private ParticleSystem targetParticleSystem;

    [Header("Spawn")]
    [SerializeField, Min(1)] private int maxParticles = 32;
    [SerializeField, Min(0.1f)] private float spawnRadius = 1.4f;
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
    [SerializeField] private Color idleColor = new Color(1f, 0.83f, 0.87f, 0.30f);
    [SerializeField] private Color activeColor = new Color(1f, 0.97f, 0.99f, 0.78f);

    public float CurrentParticleIntensity { get; private set; }

    private float intensityVelocity;
    private Material runtimeMaterial;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Reset()
    {
        inputRouter = FindAnyObjectByType<InputRouter>();
        visualFeedback = GetComponentInParent<DemoVisualFeedback>();
        targetParticleSystem = GetComponent<ParticleSystem>();
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

        ConfigureParticleSystem();
    }

    private void OnEnable()
    {
        intensityVelocity = 0f;
        CurrentParticleIntensity = 0f;
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

        float targetIntensity = visualFeedback != null
            ? visualFeedback.CurrentIntensity
            : inputRouter != null ? inputRouter.MotionMagnitude : 0f;

        CurrentParticleIntensity = Mathf.SmoothDamp(
            CurrentParticleIntensity,
            Mathf.Clamp01(targetIntensity),
            ref intensityVelocity,
            responseSmoothTime);

        ApplyIntensity(CurrentParticleIntensity);
    }

    public void SetInputRouter(InputRouter router)
    {
        inputRouter = router;
    }

    public void SetVisualFeedback(DemoVisualFeedback feedback)
    {
        visualFeedback = feedback;
    }

    private void ConfigureParticleSystem()
    {
        if (targetParticleSystem == null)
        {
            return;
        }

        ParticleSystem.MainModule main = targetParticleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
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
        ApplyIntensity(CurrentParticleIntensity);
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

    private void ApplyIntensity(float intensity)
    {
        if (targetParticleSystem == null)
        {
            return;
        }

        ParticleSystem.MainModule main = targetParticleSystem.main;
        main.startSpeed = idleStartSpeed + intensity * speedBoost;
        main.startColor = Color.Lerp(idleColor, activeColor, intensity);

        ParticleSystem.EmissionModule emission = targetParticleSystem.emission;
        emission.rateOverTime = idleEmissionRate + intensity * emissionBoost;

        ParticleSystem.NoiseModule noise = targetParticleSystem.noise;
        noise.strength = idleNoiseStrength + intensity * noiseBoost;

        SetMaterialColor(Color.Lerp(idleColor, activeColor, intensity));
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
}
