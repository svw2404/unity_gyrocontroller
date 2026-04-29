using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class SongModeManager : MonoBehaviour
{
    public enum SongMode
    {
        LOVE,
        YOUTH
    }

    public enum YouthState
    {
        Reality,
        Dream,
        Release
    }

    [SerializeField] private SongMode currentMode = SongMode.LOVE;
    [SerializeField] private YouthState currentYouthState = YouthState.Reality;
    [SerializeField] private InputRouter inputRouter;
    [SerializeField] private Transform primaryObject;
    [SerializeField] private DemoVisualFeedback primaryVisualFeedback;
    [SerializeField] private DemoParticleController primaryParticleController;
    [SerializeField] private CameraViewController cameraViewController;
    [SerializeField] private MultiObjectController multiObjectController;
    [SerializeField] private AudioReactiveDriver audioReactiveDriver;

    [Header("Scene Integration")]
    [SerializeField] private DemoVisualFeedback[] additionalVisualFeedbackTargets;
    [SerializeField] private Renderer[] reactiveEffectRenderers;
    [SerializeField] private Material[] reactiveEffectMaterials;
    [SerializeField] private GameObject[] loveModeTargets;
    [SerializeField] private GameObject[] youthModeTargets;
    [SerializeField] private GameObject[] realityStateTargets;
    [SerializeField] private GameObject[] dreamStateTargets;
    [SerializeField] private GameObject[] releaseStateTargets;
    [SerializeField] private bool driveAssignedEffectEmission = false;

    [Header("Switching")]
    [SerializeField] private bool autoSwitchCameraOnModeChange = true;

    public SongMode CurrentMode => currentMode;
    public YouthState CurrentYouthState => currentYouthState;
    public string CurrentModeLabel => currentMode.ToString();
    public string CurrentYouthStateLabel => currentMode == SongMode.YOUTH ? currentYouthState.ToString() : "-";
    public string CurrentCameraViewLabel => cameraViewController != null ? cameraViewController.CurrentViewLabel : "-";
    public float CurrentAudioEnergy => audioReactiveDriver != null ? audioReactiveDriver.CurrentAudioEnergy : 0f;
    public string CurrentAudioLabel => audioReactiveDriver != null ? audioReactiveDriver.ActiveClipLabel : "Simulated BPM Only";

    private readonly MaterialPropertyBlock effectPropertyBlock = new MaterialPropertyBlock();
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int EffectIntensityId = Shader.PropertyToID("_EffectIntensity");
    private static readonly int AudioEnergyId = Shader.PropertyToID("_AudioEnergy");
    private static readonly int RhythmPulseId = Shader.PropertyToID("_RhythmPulse");
    private static readonly int MotionIntensityId = Shader.PropertyToID("_MotionIntensity");
    private static readonly int ModeIndexId = Shader.PropertyToID("_ModeIndex");
    private static readonly int YouthStateIndexId = Shader.PropertyToID("_YouthStateIndex");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        EnsureDependencies();
        ApplyCurrentConfiguration(true);
    }

    private void OnEnable()
    {
        EnsureDependencies();
        ApplyCurrentConfiguration(true);
    }

    private void Start()
    {
        ApplyCurrentConfiguration(true);
    }

    private void Update()
    {
        HandleInput();
        PushReactiveEnergy();
    }

    public void SetSceneReferences(
        InputRouter router,
        Transform targetObject,
        DemoVisualFeedback visualFeedback,
        DemoParticleController particleController,
        CameraViewController viewController,
        MultiObjectController objectController,
        AudioReactiveDriver reactiveDriver)
    {
        inputRouter = router;
        primaryObject = targetObject;
        primaryVisualFeedback = visualFeedback;
        primaryParticleController = particleController;
        cameraViewController = viewController;
        multiObjectController = objectController;
        audioReactiveDriver = reactiveDriver;
        ApplyCurrentConfiguration(true);
    }

    private void EnsureDependencies()
    {
        AutoAssignReferences();

        if (audioReactiveDriver == null)
        {
            audioReactiveDriver = GetComponent<AudioReactiveDriver>();
            if (audioReactiveDriver == null)
            {
                audioReactiveDriver = gameObject.AddComponent<AudioReactiveDriver>();
            }
        }

        if (multiObjectController == null)
        {
            multiObjectController = GetComponent<MultiObjectController>();
            if (multiObjectController == null)
            {
                multiObjectController = gameObject.AddComponent<MultiObjectController>();
            }
        }

        if (cameraViewController == null)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = FindFirstObjectByType<Camera>();
            }

            if (camera != null)
            {
                cameraViewController = camera.GetComponent<CameraViewController>();
                if (cameraViewController == null)
                {
                    cameraViewController = camera.gameObject.AddComponent<CameraViewController>();
                }
            }
        }

        if (cameraViewController != null)
        {
            CameraMover mover = cameraViewController.GetComponent<CameraMover>();
            if (mover == null)
            {
                mover = FindAnyObjectByType<CameraMover>();
            }

            cameraViewController.SetCameraMover(mover);
            cameraViewController.SetFocusTarget(primaryObject);
        }

        if (multiObjectController != null)
        {
            multiObjectController.SetCentralObject(primaryObject);
            multiObjectController.SetInputRouter(inputRouter);
            multiObjectController.SetVisualFeedback(primaryVisualFeedback);
        }
    }

    private void AutoAssignReferences()
    {
        if (inputRouter == null)
        {
            inputRouter = FindAnyObjectByType<InputRouter>();
        }

        if (primaryObject == null)
        {
            ObjectRotator rotator = FindAnyObjectByType<ObjectRotator>();
            if (rotator != null)
            {
                primaryObject = rotator.transform;
            }
        }

        if (primaryVisualFeedback == null)
        {
            primaryVisualFeedback = FindAnyObjectByType<DemoVisualFeedback>();
        }

        if (primaryParticleController == null)
        {
            primaryParticleController = FindAnyObjectByType<DemoParticleController>();
        }

        if (cameraViewController == null)
        {
            cameraViewController = FindAnyObjectByType<CameraViewController>();
        }

        if (multiObjectController == null)
        {
            multiObjectController = FindAnyObjectByType<MultiObjectController>();
        }

        if (audioReactiveDriver == null)
        {
            audioReactiveDriver = FindAnyObjectByType<AudioReactiveDriver>();
        }
    }

    private void HandleInput()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;

        if ((keyboard != null && keyboard.tabKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame))
        {
            CycleMode();
        }

        if (currentMode == SongMode.YOUTH)
        {
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                {
                    SetYouthState(YouthState.Reality);
                }
                else if (keyboard.digit2Key.wasPressedThisFrame)
                {
                    SetYouthState(YouthState.Dream);
                }
                else if (keyboard.digit3Key.wasPressedThisFrame)
                {
                    SetYouthState(YouthState.Release);
                }
            }

            if (gamepad != null && gamepad.leftStickButton.wasPressedThisFrame)
            {
                CycleYouthState();
            }
        }

        if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
        {
            cameraViewController?.CycleView(-1);
        }
        else if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            cameraViewController?.CycleView(1);
        }

        if (gamepad != null && gamepad.rightStickButton.wasPressedThisFrame)
        {
            cameraViewController?.CycleView(1);
        }
    }

    private void CycleMode()
    {
        currentMode = currentMode == SongMode.LOVE ? SongMode.YOUTH : SongMode.LOVE;
        ApplyCurrentConfiguration(true);
    }

    private void CycleYouthState()
    {
        int next = ((int)currentYouthState + 1) % System.Enum.GetValues(typeof(YouthState)).Length;
        SetYouthState((YouthState)next);
    }

    private void SetYouthState(YouthState state)
    {
        if (currentYouthState == state)
        {
            return;
        }

        currentYouthState = state;
        ApplyCurrentConfiguration(true);
    }

    private void ApplyCurrentConfiguration(bool forceCameraView)
    {
        EnsureDependencies();

        if (audioReactiveDriver != null)
        {
            audioReactiveDriver.SetMode(currentMode);
        }

        DemoVisualFeedback.VisualTuning tuning = BuildVisualTuning(currentMode, currentYouthState);
        foreach (DemoVisualFeedback visualTarget in EnumerateVisualTargets())
        {
            visualTarget.ApplyRuntimeTuning(tuning);
        }

        if (primaryParticleController != null)
        {
            primaryParticleController.ApplyRuntimeTuning(BuildParticleTuning(currentMode, currentYouthState));
        }

        if (multiObjectController != null)
        {
            multiObjectController.ApplyMode(currentMode, currentYouthState);
        }

        if (forceCameraView && autoSwitchCameraOnModeChange && cameraViewController != null)
        {
            cameraViewController.SnapToView(GetDefaultView(currentMode, currentYouthState));
        }

        UpdateTargetGroupVisibility();
        PushReactiveEnergy();
    }

    private void PushReactiveEnergy()
    {
        float audioEnergy = audioReactiveDriver != null ? audioReactiveDriver.CurrentAudioEnergy : 0f;
        foreach (DemoVisualFeedback visualTarget in EnumerateVisualTargets())
        {
            visualTarget.SetExternalAudioEnergy(audioEnergy);
        }

        multiObjectController?.SetAudioEnergy(audioEnergy);
        ApplyReactiveSceneEffects();
    }

    private DemoVisualFeedback.VisualTuning BuildVisualTuning(SongMode mode, YouthState youthState)
    {
        switch (mode)
        {
            case SongMode.YOUTH:
                switch (youthState)
                {
                    case YouthState.Dream:
                        return new DemoVisualFeedback.VisualTuning
                        {
                            surfaceBaseColor = new Color(0.30f, 0.26f, 0.40f),
                            peakColor = new Color(0.66f, 0.84f, 0.92f),
                            idleBaselineIntensity = 0.08f,
                            idlePulseIntensity = 0.04f,
                            inputIntensityMultiplier = 0.72f,
                            audioEnergyWeight = 0.52f,
                            maxAudioContribution = 0.16f,
                            intensitySmoothTime = 0.18f,
                            simulatedBpm = 84f,
                            rhythmPulseStrength = 0.16f,
                            rhythmScaleStrength = 0.024f,
                            rhythmBrightnessStrength = 0.16f,
                            idleBreathSpeed = 0.36f,
                            idleBreathScale = 0.016f,
                            inputScaleStrength = 0.11f,
                            colorBlendStrength = 0.72f,
                            emissionStrength = 0.55f
                        };

                    case YouthState.Release:
                        return new DemoVisualFeedback.VisualTuning
                        {
                            surfaceBaseColor = new Color(0.72f, 0.84f, 0.90f),
                            peakColor = new Color(0.90f, 0.96f, 0.99f),
                            idleBaselineIntensity = 0.06f,
                            idlePulseIntensity = 0.03f,
                            inputIntensityMultiplier = 0.38f,
                            audioEnergyWeight = 0.30f,
                            maxAudioContribution = 0.11f,
                            intensitySmoothTime = 0.20f,
                            simulatedBpm = 84f,
                            rhythmPulseStrength = 0.07f,
                            rhythmScaleStrength = 0.014f,
                            rhythmBrightnessStrength = 0.07f,
                            idleBreathSpeed = 0.28f,
                            idleBreathScale = 0.022f,
                            inputScaleStrength = 0.07f,
                            colorBlendStrength = 0.48f,
                            emissionStrength = 0.22f
                        };

                    default:
                        return new DemoVisualFeedback.VisualTuning
                        {
                            surfaceBaseColor = new Color(0.20f, 0.24f, 0.30f),
                            peakColor = new Color(0.52f, 0.58f, 0.66f),
                            idleBaselineIntensity = 0.025f,
                            idlePulseIntensity = 0.018f,
                            inputIntensityMultiplier = 0.34f,
                            audioEnergyWeight = 0.22f,
                            maxAudioContribution = 0.08f,
                            intensitySmoothTime = 0.24f,
                            simulatedBpm = 84f,
                            rhythmPulseStrength = 0.045f,
                            rhythmScaleStrength = 0.010f,
                            rhythmBrightnessStrength = 0.04f,
                            idleBreathSpeed = 0.20f,
                            idleBreathScale = 0.010f,
                            inputScaleStrength = 0.05f,
                            colorBlendStrength = 0.28f,
                            emissionStrength = 0.08f
                        };
                }

            default:
                return new DemoVisualFeedback.VisualTuning
                {
                    surfaceBaseColor = new Color(0.94f, 0.58f, 0.66f),
                    peakColor = new Color(1f, 0.95f, 0.98f),
                    idleBaselineIntensity = 0.10f,
                    idlePulseIntensity = 0.08f,
                    inputIntensityMultiplier = 0.55f,
                    audioEnergyWeight = 0.48f,
                    maxAudioContribution = 0.18f,
                    intensitySmoothTime = 0.15f,
                    simulatedBpm = 85f,
                    rhythmPulseStrength = 0.14f,
                    rhythmScaleStrength = 0.028f,
                    rhythmBrightnessStrength = 0.16f,
                    idleBreathSpeed = 0.34f,
                    idleBreathScale = 0.03f,
                    inputScaleStrength = 0.10f,
                    colorBlendStrength = 0.68f,
                    emissionStrength = 0.58f
                };
        }
    }

    private DemoParticleController.ParticleTuning BuildParticleTuning(SongMode mode, YouthState youthState)
    {
        switch (mode)
        {
            case SongMode.YOUTH:
                switch (youthState)
                {
                    case YouthState.Dream:
                        return new DemoParticleController.ParticleTuning
                        {
                            maxParticles = 38,
                            orbitBallCount = 9,
                            spawnRadius = 1.55f,
                            orbitSpeed = 0.30f,
                            orbitTurbulenceStrength = 0.03f,
                            orbitBallScale = 0.16f,
                            particleLifetime = 6.0f,
                            particleSize = 0.12f,
                            idleStartSpeed = 0.07f,
                            idleEmissionRate = 8f,
                            idleNoiseStrength = 0.03f,
                            responseSmoothTime = 0.18f,
                            speedBoost = 0.12f,
                            emissionBoost = 6f,
                            noiseBoost = 0.04f,
                            rhythmExpansionStrength = 0.26f,
                            audioExpansionStrength = 0.14f,
                            motionExpansionStrength = 0.14f,
                            brightnessStrength = 0.60f,
                            movementSpeedMultiplier = 0.55f,
                            maxExpansion = 0.50f,
                            idleColor = new Color(0.34f, 0.42f, 0.72f, 0.22f),
                            activeColor = new Color(0.68f, 0.82f, 0.90f, 0.72f)
                        };

                    case YouthState.Release:
                        return new DemoParticleController.ParticleTuning
                        {
                            maxParticles = 28,
                            orbitBallCount = 7,
                            spawnRadius = 1.30f,
                            orbitSpeed = 0.12f,
                            orbitTurbulenceStrength = 0.008f,
                            orbitBallScale = 0.14f,
                            particleLifetime = 6.2f,
                            particleSize = 0.11f,
                            idleStartSpeed = 0.06f,
                            idleEmissionRate = 6f,
                            idleNoiseStrength = 0.015f,
                            responseSmoothTime = 0.22f,
                            speedBoost = 0.06f,
                            emissionBoost = 3.2f,
                            noiseBoost = 0.02f,
                            rhythmExpansionStrength = 0.12f,
                            audioExpansionStrength = 0.07f,
                            motionExpansionStrength = 0.08f,
                            brightnessStrength = 0.32f,
                            movementSpeedMultiplier = 0.38f,
                            maxExpansion = 0.24f,
                            idleColor = new Color(0.78f, 0.88f, 0.96f, 0.20f),
                            activeColor = new Color(0.90f, 0.95f, 0.99f, 0.48f)
                        };

                    default:
                        return new DemoParticleController.ParticleTuning
                        {
                            maxParticles = 24,
                            orbitBallCount = 6,
                            spawnRadius = 1.10f,
                            orbitSpeed = 0.14f,
                            orbitTurbulenceStrength = 0.012f,
                            orbitBallScale = 0.13f,
                            particleLifetime = 5.8f,
                            particleSize = 0.11f,
                            idleStartSpeed = 0.035f,
                            idleEmissionRate = 4.5f,
                            idleNoiseStrength = 0.012f,
                            responseSmoothTime = 0.26f,
                            speedBoost = 0.025f,
                            emissionBoost = 2.0f,
                            noiseBoost = 0.010f,
                            rhythmExpansionStrength = 0.10f,
                            audioExpansionStrength = 0.06f,
                            motionExpansionStrength = 0.08f,
                            brightnessStrength = 0.22f,
                            movementSpeedMultiplier = 0.40f,
                            maxExpansion = 0.26f,
                            idleColor = new Color(0.38f, 0.44f, 0.52f, 0.16f),
                            activeColor = new Color(0.58f, 0.64f, 0.72f, 0.34f)
                        };
                }

            default:
                return new DemoParticleController.ParticleTuning
                {
                    maxParticles = 30,
                    spawnRadius = 1.08f,
                    orbitBallCount = 8,
                    orbitSpeed = 0.18f,
                    orbitTurbulenceStrength = 0.03f,
                    orbitBallScale = 0.18f,
                    particleLifetime = 5.9f,
                    particleSize = 0.12f,
                    idleStartSpeed = 0.06f,
                    idleEmissionRate = 8f,
                    idleNoiseStrength = 0.05f,
                    responseSmoothTime = 0.20f,
                    speedBoost = 0.08f,
                    emissionBoost = 4.5f,
                    noiseBoost = 0.04f,
                    rhythmExpansionStrength = 0.26f,
                    audioExpansionStrength = 0.10f,
                    motionExpansionStrength = 0.08f,
                    brightnessStrength = 0.72f,
                    movementSpeedMultiplier = 0.72f,
                    maxExpansion = 0.38f,
                    idleColor = new Color(1f, 0.82f, 0.88f, 0.26f),
                    activeColor = new Color(1f, 0.97f, 0.99f, 0.72f)
                };
        }
    }

    private CameraViewController.CameraView GetDefaultView(SongMode mode, YouthState youthState)
    {
        if (mode == SongMode.LOVE)
        {
            return CameraViewController.CameraView.Wide;
        }

        switch (youthState)
        {
            case YouthState.Dream:
                return CameraViewController.CameraView.Side;
            case YouthState.Release:
                return CameraViewController.CameraView.Close;
            default:
                return CameraViewController.CameraView.Wide;
        }
    }

    private IEnumerable<DemoVisualFeedback> EnumerateVisualTargets()
    {
        HashSet<DemoVisualFeedback> yielded = new HashSet<DemoVisualFeedback>();

        if (primaryVisualFeedback != null && yielded.Add(primaryVisualFeedback))
        {
            yield return primaryVisualFeedback;
        }

        if (additionalVisualFeedbackTargets == null)
        {
            yield break;
        }

        for (int i = 0; i < additionalVisualFeedbackTargets.Length; i++)
        {
            DemoVisualFeedback target = additionalVisualFeedbackTargets[i];
            if (target != null && yielded.Add(target))
            {
                yield return target;
            }
        }
    }

    private void UpdateTargetGroupVisibility()
    {
        SetGroupActive(loveModeTargets, currentMode == SongMode.LOVE);
        SetGroupActive(youthModeTargets, currentMode == SongMode.YOUTH);
        SetGroupActive(realityStateTargets, currentMode == SongMode.YOUTH && currentYouthState == YouthState.Reality);
        SetGroupActive(dreamStateTargets, currentMode == SongMode.YOUTH && currentYouthState == YouthState.Dream);
        SetGroupActive(releaseStateTargets, currentMode == SongMode.YOUTH && currentYouthState == YouthState.Release);
    }

    private void SetGroupActive(GameObject[] group, bool active)
    {
        if (group == null)
        {
            return;
        }

        for (int i = 0; i < group.Length; i++)
        {
            if (group[i] != null)
            {
                group[i].SetActive(active);
            }
        }
    }

    private void ApplyReactiveSceneEffects()
    {
        DemoVisualFeedback referenceVisual = primaryVisualFeedback;
        if (referenceVisual == null)
        {
            foreach (DemoVisualFeedback visualTarget in EnumerateVisualTargets())
            {
                referenceVisual = visualTarget;
                break;
            }
        }

        float intensity = referenceVisual != null ? referenceVisual.CurrentIntensity : 0f;
        float rhythmPulse = referenceVisual != null ? referenceVisual.CurrentRhythmPulse : 0f;
        float motionIntensity = inputRouter != null ? inputRouter.MotionMagnitude : 0f;
        float audioEnergy = CurrentAudioEnergy;

        if (reactiveEffectRenderers != null)
        {
            for (int i = 0; i < reactiveEffectRenderers.Length; i++)
            {
                Renderer renderer = reactiveEffectRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                ApplyReactivePropertiesToRenderer(renderer, intensity, rhythmPulse, motionIntensity, audioEnergy);
            }
        }

        if (reactiveEffectMaterials != null)
        {
            for (int i = 0; i < reactiveEffectMaterials.Length; i++)
            {
                Material material = reactiveEffectMaterials[i];
                if (material == null)
                {
                    continue;
                }

                ApplyReactivePropertiesToMaterial(material, intensity, rhythmPulse, motionIntensity, audioEnergy);
            }
        }
    }

    private void ApplyReactivePropertiesToRenderer(
        Renderer renderer,
        float intensity,
        float rhythmPulse,
        float motionIntensity,
        float audioEnergy)
    {
        effectPropertyBlock.Clear();
        Material[] materials = renderer.sharedMaterials;
        bool hasAnyProperty = false;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            hasAnyProperty |= ApplyReactiveProperties(material, intensity, rhythmPulse, motionIntensity, audioEnergy, effectPropertyBlock);
        }

        if (hasAnyProperty)
        {
            renderer.SetPropertyBlock(effectPropertyBlock);
        }
    }

    private void ApplyReactivePropertiesToMaterial(
        Material material,
        float intensity,
        float rhythmPulse,
        float motionIntensity,
        float audioEnergy)
    {
        ApplyReactiveProperties(material, intensity, rhythmPulse, motionIntensity, audioEnergy, null);
    }

    private bool ApplyReactiveProperties(
        Material material,
        float intensity,
        float rhythmPulse,
        float motionIntensity,
        float audioEnergy,
        MaterialPropertyBlock propertyBlock)
    {
        bool changed = false;
        changed |= SetFloatIfPresent(material, propertyBlock, IntensityId, intensity);
        changed |= SetFloatIfPresent(material, propertyBlock, EffectIntensityId, intensity);
        changed |= SetFloatIfPresent(material, propertyBlock, AudioEnergyId, audioEnergy);
        changed |= SetFloatIfPresent(material, propertyBlock, RhythmPulseId, rhythmPulse);
        changed |= SetFloatIfPresent(material, propertyBlock, MotionIntensityId, motionIntensity);
        changed |= SetFloatIfPresent(material, propertyBlock, ModeIndexId, (int)currentMode);
        changed |= SetFloatIfPresent(material, propertyBlock, YouthStateIndexId, (int)currentYouthState);

        if (driveAssignedEffectEmission && material.HasProperty(EmissionColorId))
        {
            Color emissionColor = Color.white * Mathf.Clamp01(intensity + rhythmPulse * 0.25f + audioEnergy * 0.35f);
            if (propertyBlock != null)
            {
                propertyBlock.SetColor(EmissionColorId, emissionColor);
            }
            else
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColorId, emissionColor);
            }

            changed = true;
        }

        return changed;
    }

    private bool SetFloatIfPresent(Material material, MaterialPropertyBlock propertyBlock, int propertyId, float value)
    {
        if (!material.HasProperty(propertyId))
        {
            return false;
        }

        if (propertyBlock != null)
        {
            propertyBlock.SetFloat(propertyId, value);
        }
        else
        {
            material.SetFloat(propertyId, value);
        }

        return true;
    }
}
