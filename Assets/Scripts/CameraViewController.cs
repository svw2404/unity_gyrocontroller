using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class CameraViewController : MonoBehaviour
{
    public enum CameraView
    {
        Wide,
        Close,
        Side,
        Top
    }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private CameraMover cameraMover;
    [SerializeField] private Transform controlledCameraTransform;
    [SerializeField] private Transform focusTarget;
    [SerializeField] private Vector3 focusOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField] private bool autoCreateAnchorsIfMissing = true;
    [SerializeField] private bool updateAssignedAnchorsFromOffsets = false;

    [Header("Fallback Anchor Offsets")]
    [SerializeField] private Vector3 wideOffset = new Vector3(0f, 2.3f, -8.2f);
    [SerializeField] private Vector3 closeOffset = new Vector3(0f, 1.2f, -4.4f);
    [SerializeField] private Vector3 sideOffset = new Vector3(4.6f, 1.4f, -1.2f);
    [SerializeField] private Vector3 topOffset = new Vector3(0f, 7.5f, -0.25f);

    [Header("Assignable Anchors")]
    [SerializeField] private Transform wideAnchor;
    [SerializeField] private Transform closeAnchor;
    [SerializeField] private Transform sideAnchor;
    [SerializeField] private Transform topAnchor;

    private Transform anchorRoot;
    private readonly HashSet<Transform> generatedAnchors = new HashSet<Transform>();

    public CameraView CurrentView { get; private set; } = CameraView.Wide;
    public string CurrentViewLabel => CurrentView.ToString();

    private void Reset()
    {
        targetCamera = GetComponent<Camera>();
        cameraMover = GetComponent<CameraMover>();
        controlledCameraTransform = transform;
    }

    private void Awake()
    {
        AutoAssignReferences();
        EnsureAnchors();
        UpdateAnchorTransforms();
    }

    private void LateUpdate()
    {
        UpdateAnchorTransforms();
    }

    public void SetFocusTarget(Transform target)
    {
        focusTarget = target;
        UpdateAnchorTransforms();
    }

    public void SetCameraMover(CameraMover mover)
    {
        Transform previousControlledTransform = controlledCameraTransform;
        cameraMover = mover;
        if (mover == null)
        {
            return;
        }

        bool wasUsingLocalCameraTransform =
            previousControlledTransform == null ||
            previousControlledTransform == transform ||
            (targetCamera != null && previousControlledTransform == targetCamera.transform);

        if (wasUsingLocalCameraTransform)
        {
            controlledCameraTransform = mover.transform;
        }
    }

    public void SnapToView(CameraView view)
    {
        AutoAssignReferences();
        EnsureAnchors();
        UpdateAnchorTransforms();

        Transform anchor = GetAnchor(view);
        if (anchor == null)
        {
            return;
        }

        if (controlledCameraTransform == null)
        {
            controlledCameraTransform = targetCamera != null ? targetCamera.transform : transform;
        }

        CurrentView = view;
        controlledCameraTransform.position = anchor.position;
        controlledCameraTransform.rotation = anchor.rotation;
        cameraMover?.SnapToCurrentTransform();
    }

    public void CycleView(int direction)
    {
        int viewCount = System.Enum.GetValues(typeof(CameraView)).Length;
        int nextIndex = ((int)CurrentView + direction + viewCount) % viewCount;
        SnapToView((CameraView)nextIndex);
    }

    private void AutoAssignReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (cameraMover == null)
        {
            cameraMover = GetComponent<CameraMover>();
        }

        if (controlledCameraTransform == null)
        {
            controlledCameraTransform = cameraMover != null
                ? cameraMover.transform
                : targetCamera != null ? targetCamera.transform : transform;
        }

        if (focusTarget == null)
        {
            ObjectRotator rotator = FindAnyObjectByType<ObjectRotator>();
            if (rotator != null)
            {
                focusTarget = rotator.transform;
            }
        }
    }

    private void EnsureAnchors()
    {
        if (anchorRoot == null && autoCreateAnchorsIfMissing)
        {
            GameObject rootObject = GameObject.Find("Camera View Anchors");
            if (rootObject == null)
            {
                rootObject = new GameObject("Camera View Anchors");
            }

            anchorRoot = rootObject.transform;
        }

        wideAnchor = EnsureAnchor(wideAnchor, "Wide Anchor");
        closeAnchor = EnsureAnchor(closeAnchor, "Close Anchor");
        sideAnchor = EnsureAnchor(sideAnchor, "Side Anchor");
        topAnchor = EnsureAnchor(topAnchor, "Top Anchor");
    }

    private Transform EnsureAnchor(Transform anchor, string name)
    {
        if (anchor != null)
        {
            return anchor;
        }

        if (anchorRoot == null)
        {
            return null;
        }

        Transform existing = anchorRoot.Find(name);
        if (existing != null)
        {
            generatedAnchors.Add(existing);
            return existing;
        }

        GameObject anchorObject = new GameObject(name);
        anchorObject.transform.SetParent(anchorRoot, false);
        generatedAnchors.Add(anchorObject.transform);
        return anchorObject.transform;
    }

    private void UpdateAnchorTransforms()
    {
        if (focusTarget == null)
        {
            return;
        }

        Vector3 focusPoint = focusTarget.position + focusOffset;
        SetAnchorPose(wideAnchor, focusPoint, wideOffset);
        SetAnchorPose(closeAnchor, focusPoint, closeOffset);
        SetAnchorPose(sideAnchor, focusPoint, sideOffset);
        SetAnchorPose(topAnchor, focusPoint, topOffset);
    }

    private void SetAnchorPose(Transform anchor, Vector3 focusPoint, Vector3 offset)
    {
        if (anchor == null || !ShouldUpdateAnchor(anchor))
        {
            return;
        }

        anchor.position = focusPoint + offset;
        anchor.rotation = Quaternion.LookRotation(focusPoint - anchor.position, Vector3.up);
    }

    private bool ShouldUpdateAnchor(Transform anchor)
    {
        return updateAssignedAnchorsFromOffsets || generatedAnchors.Contains(anchor);
    }

    private Transform GetAnchor(CameraView view)
    {
        switch (view)
        {
            case CameraView.Close:
                return closeAnchor;
            case CameraView.Side:
                return sideAnchor;
            case CameraView.Top:
                return topAnchor;
            default:
                return wideAnchor;
        }
    }
}
