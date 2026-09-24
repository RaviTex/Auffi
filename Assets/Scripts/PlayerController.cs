using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private float maxSpeed;
    [SerializeField] private float acceleration;
    
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
        var speed = Mathf.Clamp(_rb.linearVelocity.magnitude + acceleration * Time.fixedDeltaTime, 0,  maxSpeed);
        _rb.linearVelocity = (_forward * _input.y + _right * _input.x).normalized * speed;
    }
}
