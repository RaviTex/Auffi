using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class CameraController : MonoBehaviour
{
    [Header("Options")] [SerializeField] private bool isHardLookAtPlayer;
    [SerializeField] private bool isHardLookOnlyX;
    [SerializeField] [Tooltip("Exclusive with Hard Look Options")] private bool isTiltingXWithVelocity;
    [SerializeField] private bool isCameraSizeChange;

    [SerializeField] private Transform target;
    [SerializeField] private Transform player;
    [SerializeField] private float moveLerpSpeed;
    [SerializeField] private float moveSnapDistance;
    [SerializeField] private float minSize;
    [SerializeField] private float maxSize;
    [SerializeField] private float exponent = 2f;
    [SerializeField] private float sizeLerpSpeed;
    [SerializeField] private float sizeSnapDistance;
    [SerializeField] private float minXRotation;
    [SerializeField] private float maxXRotation;

    
    private PlayerController _playerController;
    private Rigidbody _playerRb;
    private Camera _camera;

    private float _desiredSize;

    private void Start()
    {
        _playerController = player.GetComponent<PlayerController>();
        _camera = GetComponent<Camera>();
        _playerRb = player.GetComponent<Rigidbody>();
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

        var distance = Vector3.Distance(transform.position, target.position);
        transform.position = distance > moveSnapDistance
            ? Vector3.Lerp(transform.position, target.position, Time.deltaTime * moveLerpSpeed)
            : target.position;
        
        if (isCameraSizeChange)
        {
            float t = Mathf.InverseLerp(0f, 8f, _playerRb.linearVelocity.magnitude);
            float curvedT = Mathf.Pow(t, exponent);
            _desiredSize = Mathf.Lerp(minSize, maxSize, curvedT);
            
            var sizeDiff = Mathf.Abs(_desiredSize - _camera.orthographicSize);
            _camera.orthographicSize = sizeDiff > sizeSnapDistance ? Mathf.Lerp(_camera.orthographicSize, _desiredSize, Time.deltaTime * sizeLerpSpeed) : _desiredSize;
        }
    }
}