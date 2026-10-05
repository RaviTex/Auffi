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
    [Header("Camera peak with look input")]
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private float minPeak;
    [SerializeField] private float maxPeak;
    [SerializeField] private float rotationStrength;
    [SerializeField] private float pushBackStrength;
    [SerializeField] private bool isPeakInputInverted;

    
    private PlayerController _playerController;
    private Rigidbody _playerRb;
    private Camera _camera;

    private float _desiredSize;

    private Vector2 _lookInput;
    private float _smoothVelocity;

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
        if (isHardLookAtPlayer)
        {
            transform.LookAt(player);
            if (isHardLookOnlyX)
            {
                transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, -45, 0);
            }
        }
        if (isTiltingXWithVelocity)
        {
            float signedSpeed = Vector3.Dot(_playerRb.linearVelocity, (transform.forward + -transform.right).normalized);
            float t = Mathf.InverseLerp(8f, -8f, signedSpeed);
            float desiredXRotation = Mathf.Lerp(minXRotation, maxXRotation, t);
            transform.rotation = Quaternion.Euler(desiredXRotation, -45, 0);
        }

        float distance = Vector3.Distance(transform.position, cameraPositionTarget.position);
        transform.position = distance > moveSnapDistance
            ? Vector3.Lerp(transform.position, cameraPositionTarget.position, Time.deltaTime * moveLerpSpeed)
            : cameraPositionTarget.position;
        
        if (isCameraSizeChange)
        {
            float t = Mathf.InverseLerp(0f, 8f, _playerRb.linearVelocity.magnitude);
            float curvedT = Mathf.Pow(t, exponent);
            _desiredSize = Mathf.Lerp(minSize, maxSize, curvedT);
            
            float sizeDiff = Mathf.Abs(_desiredSize - _camera.orthographicSize);
            _camera.orthographicSize = sizeDiff > sizeSnapDistance ? Mathf.Lerp(_camera.orthographicSize, _desiredSize, Time.deltaTime * sizeLerpSpeed) : _desiredSize;
        }
        
        cameraRotationPivot.position = player.position;
        _lookInput = lookAction.action.ReadValue<Vector2>();

        float currentAngle = Mathf.DeltaAngle(0f, cameraRotationPivot.eulerAngles.y);
        float newX = Mathf.Clamp(currentAngle + (isPeakInputInverted ? -_lookInput.x : _lookInput.x) * rotationStrength * Time.deltaTime,
                minPeak, maxPeak);
        
        if (_lookInput.x == 0)
        {
            newX = Mathf.SmoothDamp(newX, 0, ref _smoothVelocity, 1f / pushBackStrength);
        }
        else
        {
            _smoothVelocity = 0;
        }

        cameraRotationPivot.rotation = Quaternion.Euler(0, newX, 0);
    }
}