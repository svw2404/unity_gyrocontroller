using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class AudioReactiveDriver : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip loveClip;
    [SerializeField] private AudioClip youthClip;

    [Header("Energy Analysis")]
    [SerializeField, Range(0.5f, 12f)] private float energyScale = 7f;
    [SerializeField, Range(64, 1024)] private int sampleSize = 128;
    [SerializeField, Min(0.01f)] private float energySmoothTime = 0.12f;

    public float CurrentAudioEnergy { get; private set; }
    public string ActiveClipLabel => audioSource != null && audioSource.clip != null ? audioSource.clip.name : "Simulated BPM Only";
    public bool HasAssignedClip => audioSource != null && audioSource.clip != null;
    public bool IsPlayingAssignedClip => HasAssignedClip && audioSource.isPlaying;

    private float[] sampleBuffer;
    private float energyVelocity;

    private void Reset()
    {
        EnsureAudioSource();
        AutoAssignClipsIfPossible();
    }

    private void Awake()
    {
        EnsureAudioSource();
        AutoAssignClipsIfPossible();
        EnsureSampleBuffer();
    }

    private void OnEnable()
    {
        EnsureAudioSource();
        EnsureSampleBuffer();
    }

    private void OnValidate()
    {
        EnsureAudioSource();
        AutoAssignClipsIfPossible();
        EnsureSampleBuffer();
    }

    private void Update()
    {
        float targetEnergy = 0f;
        if (audioSource != null && audioSource.isPlaying && audioSource.clip != null)
        {
            EnsureSampleBuffer();
            audioSource.GetOutputData(sampleBuffer, 0);

            float sum = 0f;
            for (int i = 0; i < sampleBuffer.Length; i++)
            {
                sum += sampleBuffer[i] * sampleBuffer[i];
            }

            float rms = Mathf.Sqrt(sum / sampleBuffer.Length);
            targetEnergy = Mathf.Clamp01(rms * energyScale);
        }

        CurrentAudioEnergy = Mathf.SmoothDamp(
            CurrentAudioEnergy,
            targetEnergy,
            ref energyVelocity,
            energySmoothTime);
    }

    public void SetMode(SongModeManager.SongMode mode)
    {
        EnsureAudioSource();
        AudioClip targetClip = mode == SongModeManager.SongMode.LOVE ? loveClip : youthClip;

        if (audioSource.clip != targetClip)
        {
            audioSource.Stop();
            audioSource.clip = targetClip;
        }

        if (targetClip == null)
        {
            CurrentAudioEnergy = 0f;
            return;
        }

        audioSource.loop = true;
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.85f;
    }

    private void EnsureSampleBuffer()
    {
        if (sampleSize < 64)
        {
            sampleSize = 64;
        }

        if (sampleBuffer == null || sampleBuffer.Length != sampleSize)
        {
            sampleBuffer = new float[sampleSize];
        }
    }

    [ContextMenu("Auto Assign Audio Clips")]
    private void AutoAssignClipsIfPossible()
    {
#if UNITY_EDITOR
        if (loveClip == null)
        {
            loveClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/twice.mp3");
        }

        if (youthClip == null)
        {
            youthClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/youth.mp3");
        }
#endif
    }
}
