using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private enum MovementMode
    {
        Instant,
        RampUp,
        Accelerate
    }

    [Header("General")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private AnimalBook book;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference interactionAction;

    [Header("Movement")]
    [Tooltip("Instant: move at max speed right away. RampUp: speed builds up while a direction is held. " +
             "Accelerate: velocity accelerates and decelerates smoothly.")]
    [SerializeField] private MovementMode movementMode = MovementMode.Accelerate;
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

    [Header("Audio")] 
    [SerializeField] private AK.Wwise.Event playPlayerCanTakePhoto;
    [SerializeField] private AK.Wwise.Event playPlayerHasTakenPhoto;
    [SerializeField] private AK.Wwise.Event playPlayerWalking;
    [SerializeField] private AK.Wwise.Event stopPlayerWalking;
    
    private const int MaxInteractablesDetected = 8;

    private Rigidbody _rb;
    private CapsuleCollider _capsule;
    private AnimalVisibilitySampler _visibility;
    private PlayerInventory _inventory;
    private InteractableOutline _outline;

    private readonly Collider[] _interactableHits = new Collider[MaxInteractablesDetected];
    private readonly List<Interactable> _nearbyInteractables = new List<Interactable>();
    private readonly RaycastHit[] _groundHits = new RaycastHit[4];

    private Interactable _focusedInteractable;
    private Vector2 _input;
    private Vector3 _forward;
    private Vector3 _right;

    private bool _isWalking;
    private bool _hasAlreadyPlayerCanTakePhoto;

    public ItemType CurrentItem => _inventory.CurrentItem;
    public bool HasApples => _inventory.HasApples;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();
        _visibility = GetComponent<AnimalVisibilitySampler>();
        _inventory = GetComponent<PlayerInventory>();
        _outline = GetComponent<InteractableOutline>();

        ConfigurePhysics();

        // Isometric movement axes, cached so turning the player does not feed back into movement.
        _forward = (transform.forward - transform.right).normalized;
        _right = (transform.forward + transform.right).normalized;
    }

    private void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsBlockingGameplay)
        {
            _input = Vector2.zero;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            if (_isWalking)
            {
                stopPlayerWalking?.Post(gameObject);
                _isWalking = false;
            }
            return;
        }

        _input = moveAction.action.ReadValue<Vector2>();
        UpdateFocusedInteractable();
    }

    private void LateUpdate()
    {
        if (HandleUiInput())
            return;

        Animal animal = null;
        bool canTakePhoto = _inventory.CurrentItem == ItemType.None &&
                            _visibility.TryGetVisibleAnimal(out animal, out _) &&
                            !animal.HasBeenPhotographed;
        if (canTakePhoto && !_hasAlreadyPlayerCanTakePhoto)
        {
            playPlayerCanTakePhoto?.Post(gameObject);
            _hasAlreadyPlayerCanTakePhoto = true;
        }
        else if (!canTakePhoto && _hasAlreadyPlayerCanTakePhoto)
        {
            _hasAlreadyPlayerCanTakePhoto = false;
        }

        UpdateOutlineTarget(canTakePhoto, animal);

        if (!interactionAction.action.WasPressedThisFrame())
            return;

        if (canTakePhoto)
            TakePhoto(animal);
        else if (_focusedInteractable != null && _focusedInteractable.IsInteractionAvailable)
        {
            _focusedInteractable.Interact(this);
        }
    }

    private bool HandleUiInput()
    {
        UIManager ui = UIManager.Instance;
        if (ui == null)
            return false;

        if (ui.IsSignOpen)
        {
            if (interactionAction.action.WasPressedThisFrame())
                ui.CloseSign();

            return true;
        }

        if (ui.IsBookOpen)
        {
            if (IsBookTogglePressed())
                ui.CloseBook();

            return true;
        }

        if (ui.IsGameOver)
            return true;

        if (IsBookTogglePressed())
        {
            ui.OpenBook();
            return true;
        }

        return false;
    }

    // Prototype shortcut
    private static bool IsBookTogglePressed()
    {
        return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
    }

    private void FixedUpdate()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsBlockingGameplay)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            return;
        }

        Vector3 velocity = GetDesiredHorizontalVelocity();

        if (CheckGrounded(out Vector3 groundNormal))
        {
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
        Vector3 inputDirection = (_forward * _input.y + _right * _input.x).normalized;

        if (movementMode == MovementMode.Instant)
            return inputDirection * maxSpeed;

        if (movementMode == MovementMode.RampUp)
        {
            float speed = Mathf.Clamp(velocity.magnitude + acceleration * Time.fixedDeltaTime, 0f, maxSpeed);
            return inputDirection * speed;
        }

        velocity += inputDirection * (acceleration * Time.fixedDeltaTime);
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

        if (inputDirection.magnitude < 0.1f)
        {
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
            if (velocity.magnitude < 0.01f)
                velocity = Vector3.zero;
        }

        if (velocity != Vector3.zero && !_isWalking)
        {
            _isWalking = true;
            playPlayerWalking?.Post(gameObject);
        }
        else if (velocity == Vector3.zero && _isWalking)
        {
            _isWalking = false;
            stopPlayerWalking?.Post(gameObject);
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

    private void UpdateFocusedInteractable()
    {
        int count = OverlapPlayerCapsule(_interactableHits);
        _nearbyInteractables.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider hit = _interactableHits[i];
            if (hit == null || !hit.isTrigger)
                continue;

            Interactable interactable = hit.GetComponentInParent<Interactable>();
            if (interactable == null || !interactable.isActiveAndEnabled ||
                !interactable.IsInteractionAvailable || _nearbyInteractables.Contains(interactable))
                continue;

            _nearbyInteractables.Add(interactable);
        }

        Interactable nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Interactable interactable in _nearbyInteractables)
        {
            float distance = (interactable.FocusPosition - transform.position).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;

            nearest = interactable;
            nearestDistance = distance;
        }

        SetFocusedInteractable(nearest);
    }

    private int OverlapPlayerCapsule(Collider[] results)
    {
        Vector3 center = transform.position + _capsule.center;
        float halfHeight = Mathf.Max(_capsule.height * 0.5f - _capsule.radius, 0f);
        Vector3 top = center + Vector3.up * halfHeight;
        Vector3 bottom = center - Vector3.up * halfHeight;

        return Physics.OverlapCapsuleNonAlloc(top, bottom, _capsule.radius, results, ~0,
            QueryTriggerInteraction.Collide);
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

    private void UpdateOutlineTarget(bool canTakePhoto, Animal animal)
    {
        if (_outline == null)
            return;

        if (canTakePhoto)
            _outline.SetTarget(animal);
        else
            _outline.SetTarget(_focusedInteractable);
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

    private void TakePhoto(Animal animal)
    {
        if (animal == null)
            return;

        animal.MarkPhotographed();

        if (animal.Definition != null && book != null)
            book.Unlock(animal.Definition);
        playPlayerHasTakenPhoto?.Post(gameObject);
    }
}
