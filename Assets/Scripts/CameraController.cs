using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [HideInInspector] public bool isCursorVisible;

    [Header("Options")] [SerializeField] private bool isHardLookAtPlayer;
    [SerializeField] private bool isHardLookOnlyX;
    [SerializeField] [Tooltip("Exclusive with Hard Look Options")] private bool isTiltingXWithVelocity;
    [SerializeField] private bool isCameraSizeChange;

    [SerializeField] private Transform cameraPositionTarget;
    [SerializeField] private Transform cameraRotationPivot;
    [SerializeField] private Transform player;

    [Header("Orbit")]
    [Tooltip("X rotation of the view in degrees. Changing it swings the camera along an arc around the player, who stays centered.")]
    [Range(MinPitch, MaxPitch)]
    [SerializeField] private float pitch = 45f;
    [Tooltip("Y rotation of the view in degrees.")]
    [SerializeField] private float baseYaw = -45f;
    [Tooltip("Distance from the player. Orthographic, so it does not change the framing.")]
    [SerializeField] private float distance = 25f;
    [Tooltip("How fast the camera swings to a changed pitch. 0 is instant.")]
    [SerializeField] private float pitchLerpSpeed = 6f;

    [Header("Camera move with player")]
    [SerializeField] private float moveLerpSpeed;
    [SerializeField] private float moveSnapDistance;

    [Header("Camera size lerp with move speed")]
    [SerializeField] private float minSize;
    [SerializeField] private float maxSize;
    [SerializeField] private float exponent = 2f;
    [SerializeField] private float sizeLerpSpeed;
    [SerializeField] private float sizeSnapDistance;

    [Header("Camera rotate into up down player velocity")]
    [SerializeField] private float minXRotation;
    [SerializeField] private float maxXRotation;

    [Header("Camera peak input")]
    [SerializeField] private InputActionReference peakAction;
    [SerializeField] private float minPeak;
    [SerializeField] private float maxPeak;
    [Tooltip("Seconds of holding the peak input needed to reach the max peak angle.")]
    [SerializeField] private float timeToMaxPeak = 1f;
    [Tooltip("Normalized peak angle over normalized hold time. X: 0-1 hold progress, Y: 0-1 peak angle.")]
    [SerializeField] private AnimationCurve peakRotationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Seconds needed for the peak to return to center after the input is released.")]
    [SerializeField] private float timeToPushBack = 1f;
    [Tooltip("Normalized return amount over normalized push back time. X: 0-1 return progress, Y: 0-1 (0 = full peak angle, 1 = centered).")]
    [SerializeField] private AnimationCurve pushBackCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private bool isPeakInputInverted;

    private const float MinPitch = 5f;
    private const float MaxPitch = 85f;
    private const float MinDistance = 0.1f;

    private Rigidbody _playerRb;
    private Camera _camera;

    private float _desiredSize;
    private float _currentPitch;
    private float _currentPeakAngle;

    private float _peakInput;
    private float _peakProgress;
    private bool _wasPeaking;
    private float _pushBackTime;
    private float _pushBackStartAngle;

    private Vector3 _pivotPosition;

    public float Pitch
    {
        get => pitch;
        set => pitch = Mathf.Clamp(value, MinPitch, MaxPitch);
    }

    public float BaseYaw => baseYaw;

    public float Distance
    {
        get => distance;
        set => distance = Mathf.Max(MinDistance, value);
    }

    private void OnValidate()
    {
        pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
        distance = Mathf.Max(MinDistance, distance);
    }

    private void Awake()
    {
        _pivotPosition = cameraRotationPivot.position;
        _currentPitch = pitch;
    }

    private void Start()
    {
        _camera = GetComponent<Camera>();
        _playerRb = player.GetComponent<Rigidbody>();

        if (!isCursorVisible)
            Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = isCursorVisible;
    }

    private void Update()
    {
        UpdatePeak();
        UpdatePivotPosition();
        UpdateOrbit();

        if (isCameraSizeChange)
        {
            float t = Mathf.InverseLerp(0f, 8f, _playerRb.linearVelocity.magnitude);
            float curvedT = Mathf.Pow(t, exponent);
            _desiredSize = Mathf.Lerp(minSize, maxSize, curvedT);

            float sizeDiff = Mathf.Abs(_desiredSize - _camera.orthographicSize);
            _camera.orthographicSize = sizeDiff > sizeSnapDistance
                ? Mathf.Lerp(_camera.orthographicSize, _desiredSize, Time.deltaTime * sizeLerpSpeed)
                : _desiredSize;
        }
    }

    // The pivot trails the player, which is what makes the camera follow smoothly.
    private void UpdatePivotPosition()
    {
        float playerDistance = Vector3.Distance(_pivotPosition, player.position);
        _pivotPosition = playerDistance > moveSnapDistance
            ? Vector3.Lerp(_pivotPosition, player.position, Time.deltaTime * moveLerpSpeed)
            : player.position;
    }

    // Keeps the camera on an arc around the pivot, aimed straight at it, so changing the
    // orbit angles can never push the player out of the center of the view.
    private void UpdateOrbit()
    {
        cameraRotationPivot.position = _pivotPosition;
        UpdateCurrentPitch();

        float orbitPitch = isTiltingXWithVelocity ? GetVelocityPitch() : _currentPitch;
        Quaternion orbitRotation = Quaternion.Euler(orbitPitch, baseYaw + _currentPeakAngle, 0);

        cameraPositionTarget.position = _pivotPosition - orbitRotation * Vector3.forward * distance;
        transform.position = cameraPositionTarget.position;

        if (isHardLookAtPlayer)
            ApplyHardLook();
        else
            transform.rotation = orbitRotation;
    }

    // Moves the current pitch towards the target pitch so runtime changes sweep along the arc
    // with a smooth ease-out instead of snapping.
    private void UpdateCurrentPitch()
    {
        if (pitchLerpSpeed <= 0f)
        {
            _currentPitch = pitch;
            return;
        }

        float t = 1f - Mathf.Exp(-pitchLerpSpeed * Time.deltaTime);
        _currentPitch = Mathf.Lerp(_currentPitch, pitch, t);
    }

    private void UpdatePeak()
    {
        _peakInput = peakAction != null ? peakAction.action.ReadValue<float>() : 0f;
        float direction = isPeakInputInverted ? -_peakInput : _peakInput;
        bool isHolding = Mathf.Abs(direction) > 0.01f;

        if (isHolding)
        {
            // If a new hold starts while the peak is still pushing back, derive the progress
            // from the current angle so the camera continues smoothly instead of jumping.
            if (!_wasPeaking)
            {
                _wasPeaking = true;
                _peakProgress = GetProgressForAngle(_currentPeakAngle);
            }

            _pushBackTime = 0f;

            float step = Mathf.Abs(direction) * (timeToMaxPeak > 0f ? Time.deltaTime / timeToMaxPeak : 1f);
            _peakProgress = Mathf.MoveTowards(_peakProgress, Mathf.Sign(direction), step);

            float normalizedAngle = peakRotationCurve.Evaluate(Mathf.Abs(_peakProgress));
            float targetAngle = _peakProgress >= 0f ? maxPeak : minPeak;
            _currentPeakAngle = Mathf.Lerp(0f, targetAngle, normalizedAngle);
        }
        else
        {
            if (_wasPeaking)
            {
                _wasPeaking = false;
                _pushBackStartAngle = _currentPeakAngle;
                _pushBackTime = 0f;
            }

            _pushBackTime += Time.deltaTime;
            float t = timeToPushBack > 0f ? Mathf.Clamp01(_pushBackTime / timeToPushBack) : 1f;
            _currentPeakAngle = Mathf.Lerp(_pushBackStartAngle, 0f, pushBackCurve.Evaluate(t));

            if (t >= 1f)
            {
                _currentPeakAngle = 0f;
                _peakProgress = 0f;
                _pushBackStartAngle = 0f;
            }
        }

        cameraRotationPivot.rotation = Quaternion.Euler(0, _currentPeakAngle, 0);
    }

    // Finds the hold progress whose curve value matches the given angle, so a new hold can
    // resume from the current push back angle without a jump.
    private float GetProgressForAngle(float angle)
    {
        float targetAngle = angle >= 0f ? maxPeak : minPeak;
        if (Mathf.Approximately(targetAngle, 0f))
            return 0f;

        float normalizedAngle = Mathf.Clamp01(angle / targetAngle);

        const int samples = 64;
        float bestProgress = 0f;
        float bestDiff = float.MaxValue;

        for (int i = 0; i <= samples; i++)
        {
            float progress = i / (float)samples;
            float diff = Mathf.Abs(peakRotationCurve.Evaluate(progress) - normalizedAngle);

            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestProgress = progress;
            }
        }

        return bestProgress * Mathf.Sign(angle);
    }

    private float GetVelocityPitch()
    {
        float signedSpeed = Vector3.Dot(_playerRb.linearVelocity, (transform.forward + -transform.right).normalized);
        float t = Mathf.InverseLerp(8f, -8f, signedSpeed);
        return Mathf.Lerp(minXRotation, maxXRotation, t);
    }

    private void ApplyHardLook()
    {
        transform.LookAt(player);

        if (isHardLookOnlyX)
            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, baseYaw, 0);
    }
}
