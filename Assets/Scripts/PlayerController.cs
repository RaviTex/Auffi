using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private AnimalBook book;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference interactionAction;

    [Header("Movement")]
    [SerializeField] private bool isUsingAcceleration;
    [SerializeField] private bool hasToAccelInEveryDirection;
    [SerializeField] private float maxSpeed;
    [SerializeField] private float acceleration;
    [SerializeField] private float deceleration;
    [Tooltip("How quickly the player turns towards its travel direction or focus target.")]
    [SerializeField] private float rotationLerpSpeed = 8f;

    [Header("Gravity")]
    [SerializeField] private float gravity = 20f;
    [SerializeField] private float maxFallSpeed = 30f;
    [Tooltip("Small downward push that keeps the player glued to the ground.")]
    [SerializeField] private float groundStickSpeed = 2f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [Tooltip("Steepest ground the player can walk on.")]
    [SerializeField] private float slopeLimit = 50f;
    [SerializeField] private LayerMask groundMask = ~0;

    private Rigidbody _rb;
    private CapsuleCollider _capsule;
    private AnimalVisibilitySampler _visibility;
    private PlayerInventory _inventory;

    private readonly List<Interactable> _interactables = new List<Interactable>();
    private readonly RaycastHit[] _groundHits = new RaycastHit[4];

    private Interactable _focusedInteractable;
    private Vector2 _input;
    private Vector3 _forward;
    private Vector3 _right;

    public ItemType CurrentItem => _inventory.CurrentItem;
    public bool HasApples => _inventory.HasApples;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();
        _visibility = GetComponent<AnimalVisibilitySampler>();
        _inventory = GetComponent<PlayerInventory>();

        ConfigurePhysics();

        // Isometric movement axes, cached so turning the player does not feed back into movement.
        _forward = (transform.forward - transform.right).normalized;
        _right = (transform.forward + transform.right).normalized;
    }

    private void Start()
    {
        if (book != null)
            book.Close();
    }

    private void Update()
    {
        _input = moveAction.action.ReadValue<Vector2>();
        UpdateFocusedInteractable();

        // Prototype shortcut; replace with an InputActionReference if the book needs remapping.
        if (book != null && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            book.Toggle();
    }

    private void LateUpdate()
    {
        Animal animal = null;
        bool canTakePhoto = _inventory.CurrentItem == ItemType.None &&
                            _visibility.TryGetVisibleAnimal(out animal, out _);

        if (!interactionAction.action.WasPressedThisFrame())
            return;

        // One context button: photographing wins over interacting.
        if (canTakePhoto)
            TakePhoto(animal);
        else if (_focusedInteractable != null)
            _focusedInteractable.Interact(this);
    }

    private void FixedUpdate()
    {
        Vector3 velocity = GetDesiredHorizontalVelocity();

        if (CheckGrounded(out Vector3 groundNormal))
        {
            // Follow the ground plane so small hills can be climbed, then press down
            // slightly to stay glued and avoid bouncing down slopes.
            velocity = Vector3.ProjectOnPlane(velocity, groundNormal) - groundNormal * groundStickSpeed;
        }
        else
        {
            velocity.y = Mathf.Max(_rb.linearVelocity.y - gravity * Time.fixedDeltaTime, -maxFallSpeed);
        }

        _rb.linearVelocity = velocity;
        UpdateFacing();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Game Over"))
            gameManager.GameOver();
    }

    private void ConfigurePhysics()
    {
        // Without a frictionless material, the ground stick force creates a normal force
        // every step and friction cancels the scripted movement before the player can accelerate.
        _capsule.sharedMaterial = new PhysicsMaterial("PlayerFrictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        // Gravity is applied manually. Only Y rotation is allowed so MoveRotation can
        // interpolate smoothly and collisions cannot tip the player over.
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private Vector3 GetDesiredHorizontalVelocity()
    {
        Vector3 velocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

        if (!isUsingAcceleration)
            return (_forward * _input.y + _right * _input.x).normalized * maxSpeed;

        if (!hasToAccelInEveryDirection)
        {
            float speed = Mathf.Clamp(velocity.magnitude + acceleration * Time.fixedDeltaTime, 0f, maxSpeed);
            return (_forward * _input.y + _right * _input.x).normalized * speed;
        }

        Vector3 inputDirection = (_forward * _input.y + _right * _input.x).normalized;
        velocity += inputDirection * (acceleration * Time.fixedDeltaTime);
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

        if (inputDirection.magnitude < 0.1f)
        {
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
            if (velocity.magnitude < 0.01f)
                velocity = Vector3.zero;
        }

        return velocity;
    }

    private bool CheckGrounded(out Vector3 groundNormal)
    {
        groundNormal = Vector3.up;

        float radius = _capsule.radius * 0.9f;
        float bottomOffset = Mathf.Max(_capsule.height * 0.5f - _capsule.radius, 0f);
        Vector3 origin = transform.position + _capsule.center - Vector3.up * bottomOffset + Vector3.up * 0.05f;
        int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _groundHits,
            0.05f + groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);

        bool grounded = false;
        float nearest = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _groundHits[i];
            if (hit.collider.transform.root == transform.root)
                continue;
            if (Vector3.Angle(hit.normal, Vector3.up) > slopeLimit)
                continue;
            if (hit.distance >= nearest)
                continue;

            nearest = hit.distance;
            groundNormal = hit.normal;
            grounded = true;
        }

        return grounded;
    }

    private void UpdateFacing()
    {
        Vector3 direction;

        if (_inventory.CurrentItem == ItemType.None &&
            _visibility.TryGetVisibleAnimal(out _, out Vector3 animalCenter))
            direction = animalCenter - transform.position;
        else if (_focusedInteractable != null)
            direction = _focusedInteractable.FocusPosition - transform.position;
        else
            direction = _rb.linearVelocity;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            _rb.angularVelocity = Vector3.zero;
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRotation, rotationLerpSpeed * Time.fixedDeltaTime));
    }

    public void RegisterInteractable(Interactable interactable)
    {
        if (!_interactables.Contains(interactable))
            _interactables.Add(interactable);
    }

    public void UnregisterInteractable(Interactable interactable)
    {
        _interactables.Remove(interactable);

        if (_focusedInteractable == interactable)
            SetFocusedInteractable(null);
    }

    public void SetItem(ItemType item)
    {
        _inventory.SetItem(item);
        _visibility.Paused = item != ItemType.None;
    }

    public void SetItemPreview(ItemType item)
    {
        _inventory.SetItemPreview(item);
    }

    private void UpdateFocusedInteractable()
    {
        Interactable nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = _interactables.Count - 1; i >= 0; i--)
        {
            Interactable interactable = _interactables[i];
            if (interactable == null)
            {
                _interactables.RemoveAt(i);
                continue;
            }

            if (!interactable.isActiveAndEnabled)
                continue;

            float distance = (interactable.FocusPosition - transform.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = interactable;
                nearestDistance = distance;
            }
        }

        SetFocusedInteractable(nearest);
    }

    private void SetFocusedInteractable(Interactable interactable)
    {
        if (_focusedInteractable == interactable)
            return;

        if (_focusedInteractable != null)
            _focusedInteractable.OnUnfocus(this);

        _focusedInteractable = interactable;

        if (_focusedInteractable != null)
            _focusedInteractable.OnFocus(this);
    }

    private void TakePhoto(Animal animal)
    {
        if (animal != null && animal.Definition != null && book != null)
            book.Unlock(animal.Definition);
    }
}
