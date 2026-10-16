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
    [SerializeField] private float rotationStrength;
    [SerializeField] private float pushBackStrength;
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
    private float _smoothVelocity;

    private Vector3 _pivotPosition;

    public float Pitch
    {
        get => pitch;
        set => pitch = Mathf.Clamp(value, MinPitch, MaxPitch);
    }

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

        float currentAngle = Mathf.DeltaAngle(0f, cameraRotationPivot.eulerAngles.y);
        float peakAngle = Mathf.Clamp(
            currentAngle + (isPeakInputInverted ? -_peakInput : _peakInput) * rotationStrength * Time.deltaTime,
            minPeak, maxPeak);

        if (_peakInput == 0)
        {
            peakAngle = Mathf.SmoothDamp(peakAngle, 0, ref _smoothVelocity, 1f / pushBackStrength);
        }
        else
        {
            _smoothVelocity = 0;
        }

        cameraRotationPivot.rotation = Quaternion.Euler(0, peakAngle, 0);
        _currentPeakAngle = peakAngle;
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
