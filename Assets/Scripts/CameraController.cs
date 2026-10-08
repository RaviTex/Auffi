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

    
    private PlayerController _playerController;
    private Rigidbody _playerRb;
    private Camera _camera;

    private float _desiredSize;

    private float _peakInput;
    private float _smoothVelocity;

    private Vector3 _cameraOffset;
    private Quaternion _cameraBaseRotation;
    private Vector3 _pivotPosition;

    private void Awake()
    {
        // Captured relative to the pivot so the rig orbits correctly even when the
        // camera or its target are not parented to the pivot.
        _cameraOffset = Quaternion.Inverse(cameraRotationPivot.rotation) *
                        (cameraPositionTarget.position - cameraRotationPivot.position);
        _cameraBaseRotation = Quaternion.Inverse(cameraRotationPivot.rotation) * transform.rotation;
        _pivotPosition = cameraRotationPivot.position;
    }

    private void Start()
    {
        _playerController = player.GetComponent<PlayerController>();
        _camera = GetComponent<Camera>();
        _playerRb = player.GetComponent<Rigidbody>();
        
        if(!isCursorVisible)
            Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = isCursorVisible;
    }

    private void Update()
    {
        UpdatePeak();

        // Only the player-follow translation is lerped. The peak rotation is applied on top
        // directly, so it is not damped twice (once by the peak angle, once by the position).
        float distance = Vector3.Distance(_pivotPosition, player.position);
        _pivotPosition = distance > moveSnapDistance
            ? Vector3.Lerp(_pivotPosition, player.position, Time.deltaTime * moveLerpSpeed)
            : player.position;

        cameraRotationPivot.position = _pivotPosition;
        cameraPositionTarget.position = _pivotPosition + cameraRotationPivot.rotation * _cameraOffset;
        transform.position = cameraPositionTarget.position;

        UpdateRotation();

        if (isCameraSizeChange)
        {
            float t = Mathf.InverseLerp(0f, 8f, _playerRb.linearVelocity.magnitude);
            float curvedT = Mathf.Pow(t, exponent);
            _desiredSize = Mathf.Lerp(minSize, maxSize, curvedT);
            
            float sizeDiff = Mathf.Abs(_desiredSize - _camera.orthographicSize);
            _camera.orthographicSize = sizeDiff > sizeSnapDistance ? Mathf.Lerp(_camera.orthographicSize, _desiredSize, Time.deltaTime * sizeLerpSpeed) : _desiredSize;
        }
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
    }

    private void UpdateRotation()
    {
        if (isHardLookAtPlayer)
        {
            transform.LookAt(player);
            if (isHardLookOnlyX)
                transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, -45, 0);
            return;
        }

        if (isTiltingXWithVelocity)
        {
            float signedSpeed = Vector3.Dot(_playerRb.linearVelocity, (transform.forward + -transform.right).normalized);
            float t = Mathf.InverseLerp(8f, -8f, signedSpeed);
            float desiredXRotation = Mathf.Lerp(minXRotation, maxXRotation, t);
            transform.rotation = Quaternion.Euler(desiredXRotation, -45 + cameraRotationPivot.eulerAngles.y, 0);
            return;
        }

        transform.rotation = cameraRotationPivot.rotation * _cameraBaseRotation;
    }
}
