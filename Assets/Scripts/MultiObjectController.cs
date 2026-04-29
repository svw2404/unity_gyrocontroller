using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MultiObjectController : MonoBehaviour
{
    [SerializeField] private Transform centralObject;
    [SerializeField] private InputRouter inputRouter;
    [SerializeField] private DemoVisualFeedback visualFeedback;
    [SerializeField] private Transform companionCenter;
    [SerializeField] private bool useAssignedObjectsWhenAvailable = true;
    [SerializeField] private bool generateFallbackCompanionsIfNeeded = true;
    [SerializeField] private bool showOnlyActiveModeObjects = true;
    [SerializeField] private Transform[] loveAssignedObjects;
    [SerializeField] private Transform[] youthAssignedObjects;

    [Header("Object Setup")]
    [SerializeField] private float objectHeightOffset = 0.15f;
    [SerializeField] private float loveObjectScale = 0.42f;
    [SerializeField] private float youthObjectScale = 0.48f;

    private readonly List<Transform> companionTransforms = new List<Transform>();
    private readonly List<Renderer> companionRenderers = new List<Renderer>();
    private readonly List<float> companionBaseAngles = new List<float>();
    private readonly List<float> companionBaseHeights = new List<float>();
    private readonly List<Vector3> companionBaseScales = new List<Vector3>();

    private Transform companionRoot;
    private SongModeManager.SongMode currentMode = SongModeManager.SongMode.LOVE;
    private SongModeManager.YouthState currentYouthState = SongModeManager.YouthState.Reality;
    private float currentAudioEnergy;
    private bool usingAssignedCompanions;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        EnsureRoot();
        RebuildCompanions(GetRequiredCount());
    }

    private void Update()
    {
        if ((companionCenter == null && centralObject == null) || companionTransforms.Count == 0)
        {
            return;
        }

        UpdateCompanions();
    }

    public void SetCentralObject(Transform target)
    {
        centralObject = target;
        if (companionCenter == null)
        {
            companionCenter = target;
        }

        CaptureCompanionHomes();
        UpdateCompanions();
    }

    public void SetInputRouter(InputRouter router)
    {
        inputRouter = router;
    }

    public void SetVisualFeedback(DemoVisualFeedback feedback)
    {
        visualFeedback = feedback;
    }

    public void SetAudioEnergy(float energy)
    {
        currentAudioEnergy = Mathf.Clamp01(energy);
    }

    public void ApplyMode(SongModeManager.SongMode mode, SongModeManager.YouthState youthState)
    {
        currentMode = mode;
        currentYouthState = youthState;
        EnsureRoot();
        RebuildCompanions(GetRequiredCount());
        UpdateAssignedGroupVisibility();
        UpdateCompanions();
    }

    private void AutoAssignReferences()
    {
        if (centralObject == null)
        {
            ObjectRotator rotator = FindAnyObjectByType<ObjectRotator>();
            if (rotator != null)
            {
                centralObject = rotator.transform;
            }
        }

        if (companionCenter == null)
        {
            companionCenter = centralObject;
        }

        if (inputRouter == null)
        {
            inputRouter = FindAnyObjectByType<InputRouter>();
        }

        if (visualFeedback == null)
        {
            visualFeedback = FindAnyObjectByType<DemoVisualFeedback>();
        }
    }

    private void EnsureRoot()
    {
        if (companionRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("Song Objects");
        if (existing != null)
        {
            companionRoot = existing;
            return;
        }

        GameObject rootObject = new GameObject("Song Objects");
        rootObject.transform.SetParent(transform, false);
        companionRoot = rootObject.transform;
    }

    private void RebuildCompanions(int requiredCount)
    {
        if (UseAssignedObjectsForCurrentMode())
        {
            usingAssignedCompanions = true;
            PopulateAssignedCompanions(currentMode == SongModeManager.SongMode.LOVE ? loveAssignedObjects : youthAssignedObjects);
            CaptureCompanionHomes();

            if (companionRoot != null)
            {
                companionRoot.gameObject.SetActive(false);
            }

            return;
        }

        usingAssignedCompanions = false;

        if (!generateFallbackCompanionsIfNeeded || companionRoot == null)
        {
            companionTransforms.Clear();
            companionRenderers.Clear();
            companionBaseAngles.Clear();
            companionBaseHeights.Clear();
            companionBaseScales.Clear();
            return;
        }

        if (companionRoot != null)
        {
            companionRoot.gameObject.SetActive(true);
        }

        if (companionTransforms.Count == requiredCount && companionRoot.childCount == requiredCount)
        {
            CaptureCompanionHomes();
            return;
        }

        for (int i = companionRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(companionRoot.GetChild(i).gameObject);
        }

        companionTransforms.Clear();
        companionRenderers.Clear();

        for (int i = 0; i < requiredCount; i++)
        {
            GameObject companion = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            companion.name = $"Companion {i + 1}";
            companion.transform.SetParent(companionRoot, false);
            companionTransforms.Add(companion.transform);
            companionRenderers.Add(companion.GetComponent<Renderer>());
        }

        CaptureCompanionHomes();
    }

    private int GetRequiredCount()
    {
        return currentMode == SongModeManager.SongMode.LOVE ? 4 : 5;
    }

    private void UpdateCompanions()
    {
        if (companionTransforms.Count == 0)
        {
            return;
        }

        float visualIntensity = visualFeedback != null ? visualFeedback.CurrentIntensity : 0f;
        float motionIntensity = inputRouter != null ? inputRouter.MotionMagnitude : 0f;
        float reactiveIntensity = Mathf.Clamp01(Mathf.Max(visualIntensity, Mathf.Max(motionIntensity, currentAudioEnergy)));

        if (currentMode == SongModeManager.SongMode.LOVE)
        {
            UpdateLoveCompanions(reactiveIntensity);
            return;
        }

        UpdateYouthCompanions(reactiveIntensity);
    }

    private void UpdateLoveCompanions(float reactiveIntensity)
    {
        Color[] palette =
        {
            new Color(1f, 0.72f, 0.82f),
            new Color(0.98f, 0.86f, 0.48f),
            new Color(0.70f, 0.92f, 0.82f),
            new Color(0.74f, 0.82f, 1f)
        };

        float radius = 1.55f + reactiveIntensity * 0.18f;
        float orbitSpeed = 0.32f + reactiveIntensity * 0.10f;
        float scale = loveObjectScale + reactiveIntensity * 0.05f;
        PositionCompanions(radius, orbitSpeed, scale, palette, 0.08f, 0.80f);
    }

    private void UpdateYouthCompanions(float reactiveIntensity)
    {
        Color[] palette;
        float radius;
        float orbitSpeed;
        float scale;
        float bobAmplitude;
        float bobSpeed;

        switch (currentYouthState)
        {
            case SongModeManager.YouthState.Dream:
                palette = new[]
                {
                    new Color(0.68f, 0.48f, 0.64f),
                    new Color(0.46f, 0.74f, 0.86f),
                    new Color(0.76f, 0.70f, 0.48f),
                    new Color(0.56f, 0.62f, 0.88f),
                    new Color(0.56f, 0.80f, 0.72f)
                };
                radius = 1.50f + reactiveIntensity * 0.18f;
                orbitSpeed = 0.24f + reactiveIntensity * 0.08f;
                scale = youthObjectScale + reactiveIntensity * 0.06f;
                bobAmplitude = 0.05f;
                bobSpeed = 0.72f;
                break;

            case SongModeManager.YouthState.Release:
                palette = new[]
                {
                    new Color(0.82f, 0.95f, 1f),
                    new Color(0.72f, 0.84f, 0.98f),
                    new Color(0.90f, 0.98f, 0.97f),
                    new Color(0.76f, 0.92f, 0.86f),
                    new Color(0.92f, 0.96f, 1f)
                };
                radius = 1.28f + reactiveIntensity * 0.08f;
                orbitSpeed = 0.12f + reactiveIntensity * 0.05f;
                scale = youthObjectScale - 0.02f + reactiveIntensity * 0.04f;
                bobAmplitude = 0.02f;
                bobSpeed = 0.46f;
                break;

            default:
                palette = new[]
                {
                    new Color(0.20f, 0.24f, 0.30f),
                    new Color(0.30f, 0.36f, 0.44f),
                    new Color(0.42f, 0.48f, 0.58f),
                    new Color(0.56f, 0.62f, 0.70f),
                    new Color(0.24f, 0.28f, 0.34f)
                };
                radius = 1.05f + reactiveIntensity * 0.06f;
                orbitSpeed = 0.10f + reactiveIntensity * 0.03f;
                scale = youthObjectScale - 0.06f + reactiveIntensity * 0.02f;
                bobAmplitude = 0.01f;
                bobSpeed = 0.32f;
                break;
        }

        PositionCompanions(radius, orbitSpeed, scale, palette, bobAmplitude, bobSpeed);
    }

    private void PositionCompanions(
        float radius,
        float orbitSpeed,
        float uniformScale,
        Color[] palette,
        float bobAmplitude,
        float bobSpeed)
    {
        Transform centerTarget = companionCenter != null ? companionCenter : centralObject;
        if (centerTarget == null)
        {
            return;
        }

        Vector3 center = centerTarget.position;

        for (int i = 0; i < companionTransforms.Count; i++)
        {
            Transform companion = companionTransforms[i];
            float baseAngle = i < companionBaseAngles.Count ? companionBaseAngles[i] : (360f / companionTransforms.Count) * i;
            float baseHeight = i < companionBaseHeights.Count ? companionBaseHeights[i] : objectHeightOffset;
            Vector3 baseScale = i < companionBaseScales.Count ? companionBaseScales[i] : Vector3.one;
            float baseScaleMax = Mathf.Max(0.001f, Mathf.Max(baseScale.x, Mathf.Max(baseScale.y, baseScale.z)));

            float angle = Time.time * orbitSpeed * 45f + baseAngle;
            float radians = angle * Mathf.Deg2Rad;
            float localRadius = radius + Mathf.Sin(Time.time * 0.5f + i * 0.7f) * 0.12f;
            float bob = Mathf.Sin(Time.time * bobSpeed + i * 1.13f) * bobAmplitude;

            Vector3 targetPosition = center + new Vector3(
                Mathf.Cos(radians) * localRadius,
                baseHeight + bob,
                Mathf.Sin(radians) * localRadius);

            companion.position = Vector3.Lerp(companion.position, targetPosition, 1f - Mathf.Exp(-6f * Time.deltaTime));
            float pulse = 1f + Mathf.Sin(Time.time * 1.2f + i * 0.9f) * 0.05f;
            float scaleMultiplier = (uniformScale / baseScaleMax) * pulse;
            companion.localScale = baseScale * scaleMultiplier;

            Renderer renderer = companionRenderers[i];
            if (renderer != null)
            {
                SetRendererColor(renderer, palette[i % palette.Length]);
            }
        }
    }

    private bool UseAssignedObjectsForCurrentMode()
    {
        if (!useAssignedObjectsWhenAvailable)
        {
            return false;
        }

        return HasAnyAssigned(currentMode == SongModeManager.SongMode.LOVE ? loveAssignedObjects : youthAssignedObjects);
    }

    private bool HasAnyAssigned(Transform[] targets)
    {
        if (targets == null)
        {
            return false;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void PopulateAssignedCompanions(Transform[] source)
    {
        companionTransforms.Clear();
        companionRenderers.Clear();

        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            Transform assigned = source[i];
            if (assigned == null)
            {
                continue;
            }

            companionTransforms.Add(assigned);
            companionRenderers.Add(assigned.GetComponentInChildren<Renderer>());
        }
    }

    private void CaptureCompanionHomes()
    {
        companionBaseAngles.Clear();
        companionBaseHeights.Clear();
        companionBaseScales.Clear();

        Transform centerTarget = companionCenter != null ? companionCenter : centralObject;
        Vector3 center = centerTarget != null ? centerTarget.position : Vector3.zero;

        for (int i = 0; i < companionTransforms.Count; i++)
        {
            Transform companion = companionTransforms[i];
            Vector3 offset = companion.position - center;
            Vector3 planar = new Vector3(offset.x, 0f, offset.z);

            float angle = planar.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(planar.z, planar.x) * Mathf.Rad2Deg
                : (360f / Mathf.Max(1, companionTransforms.Count)) * i;

            companionBaseAngles.Add(angle);
            companionBaseHeights.Add(planar.sqrMagnitude > 0.0001f ? offset.y : objectHeightOffset);
            companionBaseScales.Add(companion.localScale == Vector3.zero ? Vector3.one : companion.localScale);
        }
    }

    private void UpdateAssignedGroupVisibility()
    {
        if (!showOnlyActiveModeObjects)
        {
            return;
        }

        SetGroupActive(loveAssignedObjects, currentMode == SongModeManager.SongMode.LOVE);
        SetGroupActive(youthAssignedObjects, currentMode == SongModeManager.SongMode.YOUTH);

        if (companionRoot != null)
        {
            companionRoot.gameObject.SetActive(!usingAssignedCompanions);
        }
    }

    private void SetGroupActive(Transform[] group, bool active)
    {
        if (group == null)
        {
            return;
        }

        for (int i = 0; i < group.Length; i++)
        {
            if (group[i] != null)
            {
                group[i].gameObject.SetActive(active);
            }
        }
    }

    private void SetRendererColor(Renderer renderer, Color color)
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
