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

    [Header("Bootstrap")]
    [SerializeField] private bool createCubeIfMissing = true;
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

    private GameObject demoFloor;
    private bool overlayVisible = true;

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
        }

        if (rotatingObject == null && objectRotator != null)
        {
            rotatingObject = objectRotator.transform;
        }

        if (objectRotator == null && rotatingObject != null)
        {
            objectRotator = rotatingObject.GetComponent<ObjectRotator>();
        }

        if (objectRotator == null)
        {
            objectRotator = FindAnyObjectByType<ObjectRotator>();
            if (objectRotator != null)
            {
                rotatingObject = objectRotator.transform;
            }
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
                cameraMover = demoCamera.gameObject.AddComponent<CameraMover>();
            }
        }

        if (rotatingObject == null && createCubeIfMissing)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Demo Cube";
            cube.transform.localScale = cubeScale;
            rotatingObject = cube.transform;
        }

        if (rotatingObject != null && objectRotator == null)
        {
            objectRotator = rotatingObject.GetComponent<ObjectRotator>();
            if (objectRotator == null)
            {
                objectRotator = rotatingObject.gameObject.AddComponent<ObjectRotator>();
            }
        }

        if (createFloorIfMissing)
        {
            demoFloor = GameObject.Find("Demo Floor");
            if (demoFloor == null)
            {
                demoFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                demoFloor.name = "Demo Floor";
                demoFloor.transform.localScale = floorScale;
            }

            demoFloor.transform.position = new Vector3(0f, floorY, 0f);
        }

        if (rotatingObject != null)
        {
            PlaceDemoObjectAboveFloor(rotatingObject);
        }

        if (createDirectionalLightIfMissing && FindAnyObjectByType<Light>() == null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        ApplyPresentationLookIfNeeded();
    }

    private void NormalizeSceneForDemo()
    {
        if (!normalizeSceneForDemoOnPlay || demoCamera == null || rotatingObject == null)
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
    }

    private void ApplyPresentationLookIfNeeded()
    {
        if (!applyPresentationLook)
        {
            return;
        }

        if (demoCamera != null)
        {
            demoCamera.clearFlags = CameraClearFlags.SolidColor;
            demoCamera.backgroundColor = backgroundColor;
            demoCamera.fieldOfView = 50f;
        }

        if (rotatingObject != null)
        {
            ApplyColor(rotatingObject.gameObject, cubeColor);
        }

        if (demoFloor != null)
        {
            ApplyColor(demoFloor, floorColor);
        }
    }

    private void PlaceDemoObjectAboveFloor(Transform targetTransform)
    {
        Vector3 position = cubeSpawnPosition;
        float halfHeight = targetTransform.localScale.y * 0.5f;

        Renderer targetRenderer = targetTransform.GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
        {
            halfHeight = targetRenderer.bounds.extents.y;
        }

        position.y = floorY + halfHeight + cubeGroundClearance;
        targetTransform.position = position;
    }

    // Keep shader selection local here so scene polish stays optional and easy to replace later.
    private void ApplyColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
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
            return;
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

    private void OnGUI()
    {
        if (!overlayVisible || inputRouter == null)
        {
            return;
        }

        Rect area = new Rect(12f, 12f, 560f, 244f);
        GUI.Box(area, string.Empty);

        Rect labelRect = new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 20f);
        string text =
            "Controller Demo\n" +
            $"Active Input: {inputRouter.ActiveInputLabel}   Motion: {inputRouter.MotionMagnitude:0.00}\n" +
            $"Active Device: {inputRouter.ActiveDeviceLabel}\n" +
            $"Connected Pad: {inputRouter.ConnectedGamepadLabel}\n" +
            $"Object Input: ({inputRouter.ObjectRotation.x:0.00}, {inputRouter.ObjectRotation.y:0.00})\n" +
            $"Target: {(rotatingObject != null ? rotatingObject.name : "None")}   Camera: {(demoCamera != null ? demoCamera.name : "None")}\n" +
            "Object Rotate: Arrow Keys / Gamepad Left Stick / Face Buttons\n" +
            "Camera Tilt: Hold Right Mouse + Move / Gamepad Right Stick\n" +
            "Camera Move: WASD / Gamepad D-Pad / L1-R1 / L2-R2\n" +
            "Shortcuts: F reset shot, / toggle overlay";

        GUI.Label(labelRect, text);
    }
}
