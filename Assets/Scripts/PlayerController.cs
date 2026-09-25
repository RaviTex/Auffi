using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Options")] [SerializeField] private bool isUsingAcceleration;
    [SerializeField] private bool hasToAccelInEveryDirection;

    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private float maxSpeed;
    [SerializeField] private float acceleration;
    [SerializeField] private float deceleration;

    private Vector3 _forward;
    private Vector3 _right;
    private Rigidbody _rb;

    private Vector2 _input;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        _forward = (transform.forward + -transform.right).normalized;
        _right = (transform.forward + transform.right).normalized;
        _input = moveAction.action.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        if (isUsingAcceleration)
        {
            if (!hasToAccelInEveryDirection)
            {
                var speed = Mathf.Clamp(_rb.linearVelocity.magnitude + acceleration * Time.fixedDeltaTime, 0, maxSpeed);
                _rb.linearVelocity = (_forward * _input.y + _right * _input.x).normalized * speed;
            }
            else
            {
                var inputDirection = (_forward * _input.y + _right * _input.x).normalized;
                _rb.linearVelocity += inputDirection * (acceleration * Time.fixedDeltaTime);
                _rb.linearVelocity = Vector3.ClampMagnitude(_rb.linearVelocity, maxSpeed);
                if (inputDirection.magnitude < 0.1f)
                {
                    _rb.linearVelocity = Vector3.MoveTowards(
                        _rb.linearVelocity,
                        Vector3.zero,
                        deceleration * Time.fixedDeltaTime
                    );
                    if (_rb.linearVelocity.magnitude < 0.01f && _rb.linearVelocity != Vector3.zero)
                    {
                        _rb.linearVelocity = Vector3.zero;
                    }
                }
            }
        }
        else
        {
            _rb.linearVelocity = (_forward * _input.y + _right * _input.x).normalized * maxSpeed;
        }
    }
}