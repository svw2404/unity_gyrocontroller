using UnityEngine;

[DisallowMultipleComponent]
public class CameraMover : MonoBehaviour
{
    [SerializeField] private InputRouter inputRouter;

    [Header("Translation")]
    [SerializeField] private float moveSpeed = 4.25f;
    [SerializeField, Min(0.01f)] private float moveInputSmoothTime = 0.12f;
    [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.18f;

    [Header("Tilt")]
    [SerializeField] private float yawSpeed = 95f;
    [SerializeField] private float pitchSpeed = 75f;
    [SerializeField, Min(0.01f)] private float tiltInputSmoothTime = 0.12f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-22f, 45f);
    [SerializeField, Min(1f)] private float rotationSharpness = 10f;

    private Vector2 smoothedMoveInput;
    private Vector2 moveInputVelocity;
    private Vector2 smoothedTiltInput;
    private Vector2 tiltInputVelocity;
    private Vector3 targetPosition;
    private Vector3 positionVelocity;
    private float yaw;
    private float pitch;

    private void Reset()
    {
        inputRouter = FindAnyObjectByType<InputRouter>();
    }

    private void Awake()
    {
        if (inputRouter == null)
        {
            inputRouter = FindAnyObjectByType<InputRouter>();
        }

        CacheStateFromTransform();
    }

    private void OnEnable()
    {
        CacheStateFromTransform();
    }

    private void Update()
    {
        if (inputRouter == null)
        {
            return;
        }

        smoothedMoveInput = Vector2.SmoothDamp(
            smoothedMoveInput,
            inputRouter.CameraTranslation,
            ref moveInputVelocity,
            moveInputSmoothTime);

        smoothedTiltInput = Vector2.SmoothDamp(
            smoothedTiltInput,
            inputRouter.CameraTilt,
            ref tiltInputVelocity,
            tiltInputSmoothTime);

        yaw += smoothedTiltInput.x * yawSpeed * Time.deltaTime;
        pitch -= smoothedTiltInput.y * pitchSpeed * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);

        Vector3 moveDirection = GetPlanarRight() * smoothedMoveInput.x;
        moveDirection += GetPlanarForward() * smoothedMoveInput.y;
        targetPosition += moveDirection * moveSpeed * Time.deltaTime;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref positionVelocity,
            positionSmoothTime);

        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);
        float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
    }

    public void SetInputRouter(InputRouter router)
    {
        inputRouter = router;
    }

    public void SnapToCurrentTransform()
    {
        CacheStateFromTransform();
    }

    private void CacheStateFromTransform()
    {
        Vector3 currentEuler = transform.eulerAngles;
        pitch = NormalizeSignedAngle(currentEuler.x);
        yaw = NormalizeSignedAngle(currentEuler.y);
        targetPosition = transform.position;
        smoothedMoveInput = Vector2.zero;
        moveInputVelocity = Vector2.zero;
        smoothedTiltInput = Vector2.zero;
        tiltInputVelocity = Vector2.zero;
        positionVelocity = Vector3.zero;
    }

    private Vector3 GetPlanarForward()
    {
        return Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    }

    private Vector3 GetPlanarRight()
    {
        return Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
    }

    private static float NormalizeSignedAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }
}
