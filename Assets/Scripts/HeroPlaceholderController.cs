using UnityEngine;

[DefaultExecutionOrder(-80)]
[DisallowMultipleComponent]
public class HeroPlaceholderController : MonoBehaviour
{
    [SerializeField] private SongModeManager songModeManager;
    [SerializeField] private DemoVisualFeedback visualFeedback;

    [Header("Placeholder Pieces")]
    [SerializeField] private Transform core;
    [SerializeField] private Transform leftLobe;
    [SerializeField] private Transform rightLobe;
    [SerializeField] private Transform accentOrb;
    [SerializeField] private Transform shardA;
    [SerializeField] private Transform shardB;
    [SerializeField] private Transform shardC;

    [Header("Motion")]
    [SerializeField, Min(1f)] private float poseSharpness = 8f;

    private Material sharedVisualMaterial;

    private void Reset()
    {
        BuildPlaceholderIfNeeded();
        AutoAssignReferences();
    }

    private void Awake()
    {
        BuildPlaceholderIfNeeded();
        AutoAssignReferences();
    }

    private void Update()
    {
        AutoAssignReferences();
        if (core == null)
        {
            return;
        }

        float rhythm = visualFeedback != null ? visualFeedback.CurrentRhythmPulse : 0f;
        float intensity = visualFeedback != null ? visualFeedback.CurrentIntensity : 0f;
        float audio = visualFeedback != null ? visualFeedback.ExternalAudioEnergy : 0f;

        SongModeManager.SongMode mode = songModeManager != null
            ? songModeManager.CurrentMode
            : SongModeManager.SongMode.LOVE;
        SongModeManager.YouthState youthState = songModeManager != null
            ? songModeManager.CurrentYouthState
            : SongModeManager.YouthState.Reality;

        if (mode == SongModeManager.SongMode.LOVE)
        {
            UpdateLovePose(rhythm, intensity, audio);
            return;
        }

        switch (youthState)
        {
            case SongModeManager.YouthState.Dream:
                UpdateDreamPose(rhythm, intensity, audio);
                break;

            case SongModeManager.YouthState.Release:
                UpdateReleasePose(rhythm, intensity, audio);
                break;

            default:
                UpdateRealityPose(rhythm, intensity, audio);
                break;
        }
    }

    public void SetSceneReferences(SongModeManager manager, DemoVisualFeedback feedback)
    {
        songModeManager = manager;
        visualFeedback = feedback;
    }

    public void BuildPlaceholderIfNeeded()
    {
        if (core != null &&
            leftLobe != null &&
            rightLobe != null &&
            accentOrb != null &&
            shardA != null &&
            shardB != null &&
            shardC != null)
        {
            EnsureSharedMaterial();
            return;
        }

        core = EnsurePiece("Core", PrimitiveType.Sphere, new Vector3(0f, -0.04f, 0f), Vector3.one * 0.82f);
        leftLobe = EnsurePiece("Left Lobe", PrimitiveType.Sphere, new Vector3(-0.32f, 0.22f, 0.04f), Vector3.one * 0.46f);
        rightLobe = EnsurePiece("Right Lobe", PrimitiveType.Sphere, new Vector3(0.32f, 0.22f, 0.04f), Vector3.one * 0.46f);
        accentOrb = EnsurePiece("Accent Orb", PrimitiveType.Sphere, new Vector3(0f, 0.60f, -0.06f), Vector3.one * 0.16f);
        shardA = EnsurePiece("Shard A", PrimitiveType.Cube, new Vector3(-0.34f, 0.08f, -0.22f), new Vector3(0.16f, 0.10f, 0.50f));
        shardB = EnsurePiece("Shard B", PrimitiveType.Cube, new Vector3(0.00f, 0.30f, 0.30f), new Vector3(0.14f, 0.10f, 0.42f));
        shardC = EnsurePiece("Shard C", PrimitiveType.Cube, new Vector3(0.34f, -0.02f, -0.18f), new Vector3(0.18f, 0.10f, 0.48f));

        shardA.localRotation = Quaternion.Euler(18f, -24f, 20f);
        shardB.localRotation = Quaternion.Euler(-22f, 8f, -18f);
        shardC.localRotation = Quaternion.Euler(12f, 30f, -14f);

        EnsureSharedMaterial();
    }

    private void AutoAssignReferences()
    {
        if (songModeManager == null)
        {
            songModeManager = FindAnyObjectByType<SongModeManager>();
        }

        if (visualFeedback == null)
        {
            visualFeedback = GetComponent<DemoVisualFeedback>();
        }
    }

    private Transform EnsurePiece(string pieceName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale)
    {
        Transform existing = transform.Find(pieceName);
        if (existing == null)
        {
            GameObject piece = GameObject.CreatePrimitive(primitiveType);
            piece.name = pieceName;
            piece.layer = gameObject.layer;
            piece.transform.SetParent(transform, false);
            existing = piece.transform;

            Collider collider = piece.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        existing.localPosition = localPosition;
        existing.localScale = localScale;
        return existing;
    }

    private void EnsureSharedMaterial()
    {
        if (sharedVisualMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return;
            }

            sharedVisualMaterial = new Material(shader);
            sharedVisualMaterial.color = new Color(0.96f, 0.78f, 0.84f, 1f);
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i] is ParticleSystemRenderer)
            {
                continue;
            }

            renderers[i].material = sharedVisualMaterial;
        }
    }

    private void UpdateLovePose(float rhythm, float intensity, float audio)
    {
        float heartbeat = 1f + rhythm * 0.10f + intensity * 0.04f;
        float lobeLift = 0.02f + rhythm * 0.05f;
        float lobeSpread = 0.33f + rhythm * 0.04f;
        float sparkleOrbit = Time.time * (0.55f + audio * 0.3f);

        ApplyPiece(core, new Vector3(0f, -0.06f, 0f), Vector3.one * 0.84f * heartbeat, Quaternion.identity);
        ApplyPiece(leftLobe, new Vector3(-lobeSpread, 0.22f + lobeLift, 0.05f), Vector3.one * 0.46f * heartbeat, Quaternion.identity);
        ApplyPiece(rightLobe, new Vector3(lobeSpread, 0.22f + lobeLift, 0.05f), Vector3.one * 0.46f * heartbeat, Quaternion.identity);

        Vector3 sparkleOffset = new Vector3(
            Mathf.Cos(sparkleOrbit) * 0.08f,
            0.56f + Mathf.Sin(sparkleOrbit * 0.9f) * 0.03f,
            Mathf.Sin(sparkleOrbit) * 0.06f);
        ApplyPiece(accentOrb, sparkleOffset, Vector3.one * (0.16f + rhythm * 0.04f), Quaternion.identity);

        ApplyPiece(shardA, new Vector3(-0.18f, 0.06f, -0.10f), Vector3.one * 0.03f, Quaternion.Euler(18f, -24f, 20f));
        ApplyPiece(shardB, new Vector3(0f, 0.12f, 0.10f), Vector3.one * 0.03f, Quaternion.Euler(-22f, 8f, -18f));
        ApplyPiece(shardC, new Vector3(0.18f, 0.02f, -0.08f), Vector3.one * 0.03f, Quaternion.Euler(12f, 30f, -14f));
    }

    private void UpdateRealityPose(float rhythm, float intensity, float audio)
    {
        float tension = 1f + rhythm * 0.03f + intensity * 0.02f;

        ApplyPiece(core, new Vector3(0f, -0.04f, 0f), Vector3.one * 0.76f * tension, Quaternion.identity);
        ApplyPiece(leftLobe, new Vector3(-0.18f, 0.08f, 0.03f), Vector3.one * 0.22f, Quaternion.identity);
        ApplyPiece(rightLobe, new Vector3(0.18f, 0.08f, 0.03f), Vector3.one * 0.22f, Quaternion.identity);
        ApplyPiece(accentOrb, new Vector3(0f, 0.32f, -0.02f), Vector3.one * 0.08f, Quaternion.identity);

        float shardDrift = Mathf.Sin(Time.time * 0.5f + audio) * 0.015f;
        ApplyPiece(shardA, new Vector3(-0.24f, 0.10f + shardDrift, -0.12f), new Vector3(0.16f, 0.08f, 0.38f), Quaternion.Euler(12f, -22f, 12f));
        ApplyPiece(shardB, new Vector3(0f, 0.20f - shardDrift, 0.16f), new Vector3(0.12f, 0.08f, 0.30f), Quaternion.Euler(-18f, 10f, -12f));
        ApplyPiece(shardC, new Vector3(0.24f, 0.04f + shardDrift, -0.10f), new Vector3(0.16f, 0.08f, 0.34f), Quaternion.Euler(10f, 22f, -10f));
    }

    private void UpdateDreamPose(float rhythm, float intensity, float audio)
    {
        float surge = 1f + rhythm * 0.12f + intensity * 0.05f;
        float spread = 0.46f + rhythm * 0.08f + audio * 0.05f;
        float rotationOffset = Time.time * (18f + audio * 14f);

        ApplyPiece(core, new Vector3(0f, -0.03f, 0f), Vector3.one * 0.88f * surge, Quaternion.Euler(0f, rotationOffset * 0.12f, 0f));
        ApplyPiece(leftLobe, new Vector3(-spread, 0.26f, 0.12f), Vector3.one * 0.28f * surge, Quaternion.Euler(0f, rotationOffset, 18f));
        ApplyPiece(rightLobe, new Vector3(spread, 0.18f, -0.08f), Vector3.one * 0.28f * surge, Quaternion.Euler(0f, -rotationOffset, -18f));

        Vector3 orbPosition = new Vector3(
            Mathf.Cos(Time.time * 1.6f) * 0.22f,
            0.48f + Mathf.Sin(Time.time * 1.4f) * 0.10f,
            Mathf.Sin(Time.time * 1.6f) * 0.18f);
        ApplyPiece(accentOrb, orbPosition, Vector3.one * (0.18f + rhythm * 0.05f), Quaternion.identity);

        ApplyPiece(shardA, new Vector3(-0.50f, 0.18f, -0.24f), new Vector3(0.18f, 0.08f, 0.62f) * surge, Quaternion.Euler(28f, rotationOffset * 0.45f, 28f));
        ApplyPiece(shardB, new Vector3(0f, 0.38f, 0.34f), new Vector3(0.14f, 0.08f, 0.52f) * surge, Quaternion.Euler(-34f, -rotationOffset * 0.38f, -24f));
        ApplyPiece(shardC, new Vector3(0.50f, 0.02f, -0.20f), new Vector3(0.20f, 0.08f, 0.58f) * surge, Quaternion.Euler(16f, rotationOffset * 0.42f, -22f));
    }

    private void UpdateReleasePose(float rhythm, float intensity, float audio)
    {
        float calmPulse = 1f + rhythm * 0.06f + intensity * 0.03f;
        float sway = Mathf.Sin(Time.time * 0.7f + audio) * 0.03f;

        ApplyPiece(core, new Vector3(0f, -0.05f, 0f), Vector3.one * 0.82f * calmPulse, Quaternion.identity);
        ApplyPiece(leftLobe, new Vector3(-0.26f, 0.14f + sway, 0.04f), Vector3.one * 0.30f, Quaternion.identity);
        ApplyPiece(rightLobe, new Vector3(0.26f, 0.14f - sway, 0.04f), Vector3.one * 0.30f, Quaternion.identity);
        ApplyPiece(accentOrb, new Vector3(0f, 0.46f, 0.02f), Vector3.one * 0.12f, Quaternion.identity);
        ApplyPiece(shardA, new Vector3(-0.30f, 0.10f, -0.16f), new Vector3(0.12f, 0.08f, 0.34f), Quaternion.Euler(12f, -18f, 10f));
        ApplyPiece(shardB, new Vector3(0f, 0.24f, 0.20f), new Vector3(0.10f, 0.08f, 0.28f), Quaternion.Euler(-16f, 6f, -10f));
        ApplyPiece(shardC, new Vector3(0.30f, 0.02f, -0.14f), new Vector3(0.12f, 0.08f, 0.32f), Quaternion.Euler(10f, 18f, -8f));
    }

    private void ApplyPiece(Transform piece, Vector3 localPosition, Vector3 localScale, Quaternion localRotation)
    {
        if (piece == null)
        {
            return;
        }

        float blend = 1f - Mathf.Exp(-poseSharpness * Time.deltaTime);
        piece.localPosition = Vector3.Lerp(piece.localPosition, localPosition, blend);
        piece.localScale = Vector3.Lerp(piece.localScale, localScale, blend);
        piece.localRotation = Quaternion.Slerp(piece.localRotation, localRotation, blend);
    }
}
