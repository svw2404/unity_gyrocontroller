using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class DemoManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private InputRouter inputRouter;
    [SerializeField] private Camera demoCamera;
    [SerializeField] private CameraMover cameraMover;
    [SerializeField] private Transform rotatingObject;
    [SerializeField] private ObjectRotator objectRotator;
    [SerializeField] private DemoVisualFeedback demoVisualFeedback;
    [SerializeField] private DemoParticleController demoParticleController;
    [SerializeField] private HeroPlaceholderController heroPlaceholderController;
    [SerializeField] private SongModeManager songModeManager;
    [SerializeField] private CameraViewController cameraViewController;
    [SerializeField] private MultiObjectController multiObjectController;
    [SerializeField] private AudioReactiveDriver audioReactiveDriver;

    [Header("Integration")]
    [SerializeField] private bool preserveAssignedSceneLayout = true;
    [SerializeField] private Transform particleCenter;
    [SerializeField] private Transform orbitBallParent;

    [Header("Bootstrap")]
    [SerializeField] private bool createCubeIfMissing = true;
    [SerializeField] private GameObject heroObjectPrefab;
    [SerializeField] private bool createFloorIfMissing = true;
    [SerializeField] private bool createDirectionalLightIfMissing = true;
    [SerializeField] private bool normalizeSceneForDemoOnPlay = true;
    [SerializeField] private Vector3 cubeSpawnPosition = Vector3.zero;
    [SerializeField] private Vector3 cubeScale = Vector3.one * 1.5f;
    [SerializeField] private float floorY = -0.08f;
    [SerializeField] private float cubeGroundClearance = 0.65f;
    [SerializeField, Min(1f)] private float cameraDistance = 6.5f;
    [SerializeField] private float cameraHeightOffset = 1.35f;
    [SerializeField] private Vector3 objectFocusOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Presentation")]
    [SerializeField] private bool applyPresentationLook = true;
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
    [SerializeField] private Color cubeColor = new Color(0.90f, 0.46f, 0.48f, 1f);
    [SerializeField] private Color floorColor = new Color(0.18f, 0.22f, 0.27f, 1f);
    [SerializeField] private Vector3 floorScale = new Vector3(4f, 1f, 4f);

    [Header("Demo Helpers")]
    [SerializeField] private bool enableResetShotShortcut = true;
    [SerializeField] private bool resetObjectRotationWithShot = true;
    [SerializeField] private bool showDebugOverlay = true;
    [SerializeField] private Vector2 overlayScreenOffset = new Vector2(180f, 56f);
    [SerializeField, Min(320f)] private float overlayMaxWidth = 560f;

    private GameObject demoFloor;
    private bool overlayVisible = true;
    private bool createdFallbackHero;
    private bool createdFallbackCamera;
    private bool createdFallbackFloor;
    private bool createdFallbackLight;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        EnsureSceneObjects();
        NormalizeSceneForDemo();
        WireReferences();
    }

    private void Start()
    {
        overlayVisible = showDebugOverlay;
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (enableResetShotShortcut && Keyboard.current.fKey.wasPressedThisFrame)
        {
            ResetDemoShot();
        }

        if (Keyboard.current.slashKey.wasPressedThisFrame)
        {
            overlayVisible = !overlayVisible;
        }
    }

    private void AutoAssignReferences()
    {
        if (inputRouter == null)
        {
            inputRouter = FindAnyObjectByType<InputRouter>();
        }

        if (demoCamera == null)
        {
            demoCamera = Camera.main;
        }

        if (demoCamera == null)
        {
            demoCamera = FindFirstObjectByType<Camera>();
        }

        if (cameraMover == null && demoCamera != null)
        {
            cameraMover = demoCamera.GetComponent<CameraMover>();
            if (cameraMover == null)
            {
                cameraMover = FindAnyObjectByType<CameraMover>();
            }
        }

        if (rotatingObject == null && objectRotator != null)
        {
            rotatingObject = objectRotator.transform;
        }

        if (objectRotator == null && rotatingObject != null)
        {
            objectRotator = rotatingObject.GetComponent<ObjectRotator>();
        }

        if (demoVisualFeedback == null && rotatingObject != null)
        {
            demoVisualFeedback = rotatingObject.GetComponent<DemoVisualFeedback>();
        }

        if (demoParticleController == null && rotatingObject != null)
        {
            demoParticleController = rotatingObject.GetComponentInChildren<DemoParticleController>();
        }

        if (particleCenter == null && rotatingObject != null)
        {
            particleCenter = rotatingObject;
        }

        if (heroPlaceholderController == null && rotatingObject != null)
        {
            heroPlaceholderController = rotatingObject.GetComponent<HeroPlaceholderController>();
        }

        if (songModeManager == null)
        {
            songModeManager = GetComponent<SongModeManager>();
        }

        if (cameraViewController == null && demoCamera != null)
        {
            cameraViewController = demoCamera.GetComponent<CameraViewController>();
            if (cameraViewController == null)
            {
                cameraViewController = FindAnyObjectByType<CameraViewController>();
            }
        }

        if (multiObjectController == null)
        {
            multiObjectController = GetComponent<MultiObjectController>();
        }

        if (audioReactiveDriver == null)
        {
            audioReactiveDriver = GetComponent<AudioReactiveDriver>();
        }

        if (objectRotator == null)
        {
            objectRotator = FindAnyObjectByType<ObjectRotator>();
            if (objectRotator != null)
            {
                rotatingObject = objectRotator.transform;
            }
        }

        if (demoVisualFeedback == null)
        {
            demoVisualFeedback = FindAnyObjectByType<DemoVisualFeedback>();
        }

        if (demoParticleController == null)
        {
            demoParticleController = FindAnyObjectByType<DemoParticleController>();
        }
    }

    private void EnsureSceneObjects()
    {
        if (inputRouter == null)
        {
            GameObject routerObject = new GameObject("InputRouter");
            inputRouter = routerObject.AddComponent<InputRouter>();
        }

        if (demoCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            demoCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            createdFallbackCamera = true;
        }
        else if (Camera.main == null)
        {
            demoCamera.tag = "MainCamera";
        }

        if (cameraMover == null)
        {
            cameraMover = demoCamera.GetComponent<CameraMover>();
            if (cameraMover == null)
            {
                cameraMover = FindAnyObjectByType<CameraMover>();
            }

            if (cameraMover == null)
            {
                cameraMover = demoCamera.gameObject.AddComponent<CameraMover>();
            }
        }

        if (cameraViewController == null)
        {
            cameraViewController = demoCamera.GetComponent<CameraViewController>();
            if (cameraViewController == null)
            {
                cameraViewController = FindAnyObjectByType<CameraViewController>();
            }

            if (cameraViewController == null)
            {
                cameraViewController = demoCamera.gameObject.AddComponent<CameraViewController>();
            }
        }

        if (rotatingObject == null && createCubeIfMissing)
        {
            GameObject heroObject = null;

            if (heroObjectPrefab != null)
            {
                heroObject = Instantiate(heroObjectPrefab);
                heroObject.name = heroObjectPrefab.name;
            }
            else
            {
                heroObject = CreateHeroPlaceholderObject();
            }

            if (heroObject == null)
            {
                heroObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                heroObject.name = "Demo Cube";
                heroObject.transform.localScale = cubeScale;
            }

            rotatingObject = heroObject.transform;
            createdFallbackHero = true;
        }

        if (rotatingObject != null && objectRotator == null)
        {
            objectRotator = rotatingObject.GetComponent<ObjectRotator>();
            if (objectRotator == null)
            {
                objectRotator = rotatingObject.gameObject.AddComponent<ObjectRotator>();
            }
        }

        if (rotatingObject != null && demoVisualFeedback == null)
        {
            demoVisualFeedback = rotatingObject.GetComponent<DemoVisualFeedback>();
            if (demoVisualFeedback == null)
            {
                demoVisualFeedback = rotatingObject.gameObject.AddComponent<DemoVisualFeedback>();
            }
        }

        if (rotatingObject != null && demoParticleController == null)
        {
            demoParticleController = rotatingObject.GetComponentInChildren<DemoParticleController>();
            if (demoParticleController == null)
            {
                GameObject particleObject = new GameObject("Demo Particles");
                particleObject.transform.SetParent(rotatingObject, false);
                particleObject.transform.localPosition = Vector3.zero;
                particleObject.transform.localRotation = Quaternion.identity;
                particleObject.transform.localScale = Vector3.one;
                particleObject.AddComponent<ParticleSystem>();
                demoParticleController = particleObject.AddComponent<DemoParticleController>();
            }
        }

        if (rotatingObject != null && heroPlaceholderController == null)
        {
            heroPlaceholderController = rotatingObject.GetComponent<HeroPlaceholderController>();
        }

        if (particleCenter == null && rotatingObject != null)
        {
            particleCenter = rotatingObject;
        }

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

        if (songModeManager == null)
        {
            songModeManager = GetComponent<SongModeManager>();
            if (songModeManager == null)
            {
                songModeManager = gameObject.AddComponent<SongModeManager>();
            }
        }

        bool allowFallbackSceneBootstrap = !preserveAssignedSceneLayout || createdFallbackHero || createdFallbackCamera;

        if (createFloorIfMissing && allowFallbackSceneBootstrap)
        {
            demoFloor = GameObject.Find("Demo Floor");
            if (demoFloor == null)
            {
                demoFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                demoFloor.name = "Demo Floor";
                demoFloor.transform.localScale = floorScale;
                createdFallbackFloor = true;
            }

            demoFloor.transform.position = new Vector3(0f, floorY, 0f);
        }

        if (rotatingObject != null && (!preserveAssignedSceneLayout || createdFallbackHero))
        {
            PlaceDemoObjectAboveFloor(rotatingObject);
        }

        if (createDirectionalLightIfMissing && allowFallbackSceneBootstrap && FindAnyObjectByType<Light>() == null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            createdFallbackLight = true;
        }

        ApplyPresentationLookIfNeeded();
    }

    private void NormalizeSceneForDemo()
    {
        if (!normalizeSceneForDemoOnPlay || demoCamera == null || rotatingObject == null)
        {
            return;
        }

        if (preserveAssignedSceneLayout && !createdFallbackHero && !createdFallbackCamera)
        {
            return;
        }

        rotatingObject.localRotation = Quaternion.identity;

        Vector3 focusPoint = rotatingObject.position + objectFocusOffset;
        Vector3 cameraPosition = focusPoint + new Vector3(0f, cameraHeightOffset, -cameraDistance);

        demoCamera.transform.position = cameraPosition;
        demoCamera.transform.rotation = Quaternion.LookRotation(focusPoint - cameraPosition, Vector3.up);

        if (cameraMover != null)
        {
            cameraMover.SnapToCurrentTransform();
        }

        if (objectRotator != null)
        {
            objectRotator.SnapToCurrentTransform();
        }
    }

    private void ResetDemoShot()
    {
        if (rotatingObject != null && resetObjectRotationWithShot)
        {
            rotatingObject.localRotation = Quaternion.identity;
        }

        NormalizeSceneForDemo();
    }

    private void WireReferences()
    {
        if (objectRotator != null)
        {
            objectRotator.SetInputRouter(inputRouter);
        }

        if (cameraMover != null)
        {
            cameraMover.SetInputRouter(inputRouter);
        }

        if (demoVisualFeedback != null)
        {
            demoVisualFeedback.SetInputRouter(inputRouter);
        }

        if (demoParticleController != null)
        {
            demoParticleController.SetInputRouter(inputRouter);
            demoParticleController.SetVisualFeedback(demoVisualFeedback);
            demoParticleController.SetParticleCenter(particleCenter != null ? particleCenter : rotatingObject);
            demoParticleController.SetOrbitBallParent(orbitBallParent);
        }

        if (heroPlaceholderController != null)
        {
            heroPlaceholderController.SetSceneReferences(songModeManager, demoVisualFeedback);
        }

        if (cameraViewController != null)
        {
            cameraViewController.SetCameraMover(cameraMover);
            cameraViewController.SetFocusTarget(rotatingObject);
        }

        if (multiObjectController != null)
        {
            multiObjectController.SetCentralObject(rotatingObject);
            multiObjectController.SetInputRouter(inputRouter);
            multiObjectController.SetVisualFeedback(demoVisualFeedback);
        }

        if (songModeManager != null)
        {
            songModeManager.SetSceneReferences(
                inputRouter,
                rotatingObject,
                demoVisualFeedback,
                demoParticleController,
                cameraViewController,
                multiObjectController,
                audioReactiveDriver);
        }
    }

    private void ApplyPresentationLookIfNeeded()
    {
        if (!applyPresentationLook)
        {
            return;
        }

        if (demoCamera != null && (!preserveAssignedSceneLayout || createdFallbackCamera))
        {
            demoCamera.clearFlags = CameraClearFlags.SolidColor;
            demoCamera.backgroundColor = backgroundColor;
            demoCamera.fieldOfView = 50f;
        }

        if (rotatingObject != null && (!preserveAssignedSceneLayout || createdFallbackHero))
        {
            if (heroPlaceholderController == null)
            {
                ApplyColor(rotatingObject.gameObject, cubeColor);
            }
        }

        if (demoFloor != null && (!preserveAssignedSceneLayout || createdFallbackFloor))
        {
            ApplyColor(demoFloor, floorColor);
        }
    }

    private void PlaceDemoObjectAboveFloor(Transform targetTransform)
    {
        Vector3 position = cubeSpawnPosition;
        float halfHeight = targetTransform.localScale.y * 0.5f;

        Renderer targetRenderer = targetTransform.GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            targetRenderer = targetTransform.GetComponentInChildren<Renderer>();
        }

        if (targetRenderer != null)
        {
            halfHeight = targetRenderer.bounds.extents.y;
        }

        position.y = floorY + halfHeight + cubeGroundClearance;
        targetTransform.position = position;
    }

    private GameObject CreateHeroPlaceholderObject()
    {
        GameObject heroObject = new GameObject("Hero Object");
        heroObject.transform.localScale = cubeScale;

        HeroPlaceholderController placeholder = heroObject.AddComponent<HeroPlaceholderController>();
        placeholder.BuildPlaceholderIfNeeded();
        heroPlaceholderController = placeholder;

        return heroObject;
    }

    // Keep shader selection local here so scene polish stays optional and easy to replace later.
    private void ApplyColor(GameObject target, Color color)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            Material material = renderer.sharedMaterial;
            if (material == null || material.name.StartsWith("Default-Material"))
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader != null)
                {
                    material = new Material(shader);
                    renderer.material = material;
                }
            }

            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }
    }

    private void OnGUI()
    {
        if (!overlayVisible || inputRouter == null)
        {
            return;
        }

        float overlayScale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 1f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(overlayScale, overlayScale, 1f));

        float inverseScale = 1f / overlayScale;
        float scaledScreenWidth = Screen.width * inverseScale;
        float scaledScreenHeight = Screen.height * inverseScale;
        float marginX = Mathf.Clamp(overlayScreenOffset.x, 8f, Mathf.Max(8f, scaledScreenWidth - 140f));
        float marginY = Mathf.Clamp(overlayScreenOffset.y, 8f, Mathf.Max(8f, scaledScreenHeight - 120f));
        float maxWidth = Mathf.Min(overlayMaxWidth, scaledScreenWidth - marginX - 16f);
        Rect area = new Rect(marginX, marginY, maxWidth, 304f);
        GUI.Box(area, string.Empty);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true,
            richText = false
        };

        Rect labelRect = new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f);
        string text =
            "Interactive Song Demo\n" +
            $"Input Mode: {inputRouter.ConfiguredInputModeLabel}   Source: {inputRouter.CurrentInputModeLabel}\n" +
            $"Song Mode: {(songModeManager != null ? songModeManager.CurrentModeLabel : "LOVE")}   " +
            $"Youth State: {(songModeManager != null ? songModeManager.CurrentYouthStateLabel : "-")}   " +
            $"Camera View: {(songModeManager != null ? songModeManager.CurrentCameraViewLabel : "-")}\n" +
            $"Motion Intensity: {inputRouter.MotionMagnitude:0.00}   " +
            $"Audio Energy: {(songModeManager != null ? songModeManager.CurrentAudioEnergy : 0f):0.00}\n" +
            $"Audio Clip: {(songModeManager != null ? songModeManager.CurrentAudioLabel : "Simulated BPM Only")}\n" +
            $"Device: {inputRouter.ActiveDeviceLabel}\n" +
            $"Sensors: {inputRouter.MobileSensorLabel}\n" +
            $"Phone Rotate: ({inputRouter.PhoneObjectRotation.x:0.00}, {inputRouter.PhoneObjectRotation.y:0.00})   Motion: {inputRouter.PhoneMotionMagnitude:0.00}\n" +
            $"Phone Camera: Tilt ({inputRouter.PhoneCameraTilt.x:0.00}, {inputRouter.PhoneCameraTilt.y:0.00})   Move ({inputRouter.PhoneCameraTranslation.x:0.00}, {inputRouter.PhoneCameraTranslation.y:0.00})\n" +
            $"Phone Lift: {inputRouter.PhoneObjectLift:0.00}\n" +
            $"Object Input: ({inputRouter.ObjectRotation.x:0.00}, {inputRouter.ObjectRotation.y:0.00})   Visual: {(demoVisualFeedback != null ? demoVisualFeedback.CurrentIntensity : 0f):0.00}\n" +
            $"Particles: {(demoParticleController != null ? demoParticleController.CurrentParticleIntensity : 0f):0.00}   Radius: {(demoParticleController != null ? demoParticleController.CurrentParticleRadius : 0f):0.00}   Orbit: {(demoParticleController != null ? demoParticleController.CurrentOrbitSpeed : 0f):0.00}\n" +
            "Song Mode: Tab / Gamepad Triangle   Youth State: 1 / 2 / 3 or Left Stick Press\n" +
            "Camera Views: Q / E or Right Stick Press\n" +
            "Phone: tilt iPhone to pose the cube, nudge camera tilt/move, roll to lift the cube\n" +
            "Fallback Rotate: Arrow Keys / Left Stick / Face Buttons\n" +
            "Fallback Camera: Right Mouse or Right Stick   Move: WASD / D-Pad / Shoulders / Triggers\n" +
            "Shortcuts: F reset, / overlay";

        GUI.Label(labelRect, text, labelStyle);
        GUI.matrix = previousMatrix;
    }
}
