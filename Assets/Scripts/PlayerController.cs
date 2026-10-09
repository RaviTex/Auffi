using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerController : MonoBehaviour
{
    [Header("Options")] [SerializeField] private bool isUsingAcceleration;
    [SerializeField] private bool hasToAccelInEveryDirection;

    [Header("Movement")] [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference interactionAction;
    [SerializeField] private float maxSpeed;
    [SerializeField] private float acceleration;
    [SerializeField] private float deceleration;
    [Tooltip("How quickly the player turns towards its travel direction or focus target.")]
    [SerializeField] private float rotationLerpSpeed = 8f;

    [Header("Taking Photos")] 
    [SerializeField] private float detectionDistance;
    [SerializeField] private GameObject canTakePhotoFeedbackTxt;
    [SerializeField] private AnimalBook book;

    [Header("Visibility")]
    [Tooltip("How often animal visibility is re-checked, in seconds.")]
    [SerializeField] private float visibilityCheckInterval = 0.1f;
    [Tooltip("How long the prompt stays active after an animal was last seen. Prevents flickering between checks.")]
    [SerializeField] private float visibilityGraceTime = 0.15f;
    [Tooltip("Height in pixels of the internal visibility render. Width follows the game's aspect ratio.")]
    [SerializeField] private int visibilityTextureHeight = 90;
    [Tooltip("Hides the player's own body during the check, so the player never blocks the view of an animal.")]
    [SerializeField] private bool ignorePlayer = true;
    [SerializeField] private bool debugVisibility;

    [Header("Inventory Slot")]
    [Tooltip("Shown when the player holds nothing and no camera is out.")]
    [SerializeField] private GameObject emptySlotVisual;
    [Tooltip("Shown when an animal can be photographed.")]
    [SerializeField] private GameObject cameraSlotVisual;
    [Tooltip("Shown while the player carries an empty bucket.")]
    [SerializeField] private GameObject bucketSlotVisual;
    [Tooltip("Shown while the player carries a bucket filled with apples.")]
    [SerializeField] private GameObject applesSlotVisual;

    private const int MaxAnimalsDetected = 16;

    public ItemType CurrentItem => _currentItem;
    public bool HasApples => _currentItem == ItemType.Apples;

    private Vector3 _forward;
    private Vector3 _right;
    private Rigidbody _rb;

    private Vector2 _input;

    private Camera _camera;
    private Collider[] _animalsInRange;
    private LayerMask _animalMask;
    private bool _canTakePhoto;
    private Collider _photoTarget;

    private ItemType _currentItem = ItemType.None;
    private ItemType _itemPreview = ItemType.None;
    private readonly List<Interactable> _interactables = new List<Interactable>();
    private Interactable _focusedInteractable;

    private Camera _visibilityCamera;
    private RenderTexture _visibilityTexture;
    private Texture2D _visibilityReadback;
    private Material _visibilityKeyMaterial;
    private Coroutine _visibilityRoutine;
    private float _nextVisibilityCheck;
    private float _lastVisibleTime = float.NegativeInfinity;
    private string _lastDebugMessage;
    private Comparison<Collider> _distanceComparison;

    private readonly List<Collider> _candidates = new List<Collider>();
    private readonly Plane[] _frustumPlanes = new Plane[6];
    private readonly List<Renderer> _candidateRenderers = new List<Renderer>();
    private readonly List<Renderer> _keyedRenderers = new List<Renderer>();
    private readonly List<Material[]> _originalMaterials = new List<Material[]>();
    private readonly List<Renderer> _playerRenderers = new List<Renderer>();
    private readonly List<Renderer> _hiddenPlayerRenderers = new List<Renderer>();

    private void Awake()
    {
        _animalsInRange = new Collider[MaxAnimalsDetected];
        _animalMask = LayerMask.GetMask("Animal");
        _distanceComparison = CompareByDistance;

        // The movement axes are cached so turning the player does not feed back into movement.
        _forward = (transform.forward + -transform.right).normalized;
        _right = (transform.forward + transform.right).normalized;
    }

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _camera = Camera.main;

        CreateVisibilityCamera();
        if (_visibilityCamera != null)
            _visibilityRoutine = StartCoroutine(VisibilityLoop());

        if (book != null)
            book.Close();

        UpdateSlotVisual();
    }

    private void OnDestroy()
    {
        if (_visibilityRoutine != null)
            StopCoroutine(_visibilityRoutine);

        DisposeVisibilityResources();
    }

    private void Update()
    {
        _input = moveAction.action.ReadValue<Vector2>();

        UpdateFocusedInteractable();

        // Direct key binding for the prototype; swap for an InputActionReference if it needs remapping.
        if (book != null && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            book.Toggle();
    }

    private void LateUpdate()
    {
        CanTakePhoto();
        UpdateFacing();
        UpdateSlotVisual();

        canTakePhotoFeedbackTxt.SetActive(_canTakePhoto);

        // One context button: photographing wins over picking up.
        if (!interactionAction.action.WasPressedThisFrame())
            return;

        if (_canTakePhoto)
            TakePhoto();
        else if (_focusedInteractable != null)
            TryInteract();
    }

    private void FixedUpdate()
    {
        if (isUsingAcceleration)
        {
            if (!hasToAccelInEveryDirection)
            {
                float speed = Mathf.Clamp(_rb.linearVelocity.magnitude + acceleration * Time.fixedDeltaTime, 0,
                    maxSpeed);
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
        _currentItem = item;
    }

    public void SetItemPreview(ItemType item)
    {
        _itemPreview = item;
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

    private void TryInteract()
    {
        if (_focusedInteractable != null)
            _focusedInteractable.Interact(this);
    }

    private void UpdateFacing()
    {
        Vector3 direction;

        if (_canTakePhoto && _photoTarget != null)
            direction = _photoTarget.bounds.center - transform.position;
        else if (_focusedInteractable != null)
            direction = _focusedInteractable.FocusPosition - transform.position;
        else
            direction = _rb.linearVelocity;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        _rb.rotation = Quaternion.Slerp(_rb.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
    }

    private void UpdateSlotVisual()
    {
        ItemType state = _currentItem != ItemType.None
            ? _currentItem
            : _canTakePhoto
                ? ItemType.Camera
                : _itemPreview != ItemType.None
                    ? _itemPreview
                    : ItemType.None;

        SetSlotVisual(emptySlotVisual, state == ItemType.None);
        SetSlotVisual(cameraSlotVisual, state == ItemType.Camera);
        SetSlotVisual(bucketSlotVisual, state == ItemType.Bucket);
        SetSlotVisual(applesSlotVisual, state == ItemType.Apples);
    }

    private static void SetSlotVisual(GameObject visual, bool active)
    {
        if (visual != null && visual.activeSelf != active)
            visual.SetActive(active);
    }

    private void CanTakePhoto()
    {
        int numOfColliders = Physics.OverlapSphereNonAlloc(transform.position, detectionDistance, _animalsInRange,
            _animalMask);
        GatherCandidates(numOfColliders);

        bool isVisible = _currentItem == ItemType.None && _visibilityCamera != null && _photoTarget != null;

        if (isVisible)
            _lastVisibleTime = Time.time;

        _canTakePhoto = Time.time - _lastVisibleTime <= visibilityGraceTime;

        if (debugVisibility && !isVisible)
            LogVisibilityDebug(numOfColliders);
    }

    // Nearest first, one entry per animal (an animal can have several colliders in range).
    private void GatherCandidates(int numOfColliders)
    {
        _candidates.Clear();

        for (int i = 0; i < numOfColliders; i++)
        {
            Collider candidate = _animalsInRange[i];
            if (candidate == null || ContainsAnimal(candidate.transform.root))
                continue;

            _candidates.Add(candidate);
        }

        _candidates.Sort(_distanceComparison);
    }

    private bool ContainsAnimal(Transform root)
    {
        foreach (Collider candidate in _candidates)
        {
            if (candidate != null && candidate.transform.root == root)
                return true;
        }

        return false;
    }

    private int CompareByDistance(Collider a, Collider b)
    {
        float distanceA = (a.bounds.center - transform.position).sqrMagnitude;
        float distanceB = (b.bounds.center - transform.position).sqrMagnitude;
        return distanceA.CompareTo(distanceB);
    }

    private void TakePhoto()
    {
        if (_photoTarget == null || book == null)
            return;

        Animal animal = _photoTarget.GetComponentInParent<Animal>();
        if (animal != null && animal.Definition != null)
            book.Unlock(animal.Definition);
    }

    // Creates a hidden camera that mirrors the main camera and renders into a small texture.
    // The investigated animal is drawn with an unlit key material, so any key-colored pixel in
    // the readback means the animal is actually visible. The scene's normal depth testing handles
    // occlusion using the real rendered meshes instead of their (often mismatched) colliders.
    private void CreateVisibilityCamera()
    {
        if (_camera == null)
            return;

        Shader keyShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (keyShader == null)
        {
            Debug.LogError("[PlayerController] URP unlit shader not found. Animal visibility is disabled.");
            return;
        }

        int height = Mathf.Max(16, visibilityTextureHeight);
        int width = Mathf.Max(16, Mathf.RoundToInt(height * _camera.aspect));

        _visibilityTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "AnimalVisibility",
            filterMode = FilterMode.Point,
            antiAliasing = 1,
            useMipMap = false
        };
        _visibilityTexture.Create();

        _visibilityReadback = new Texture2D(width, height, TextureFormat.RGBA32, false);

        _visibilityKeyMaterial = new Material(keyShader)
        {
            name = "AnimalVisibilityKey",
            renderQueue = 2450
        };
        if (_visibilityKeyMaterial.HasProperty("_BaseColor"))
            _visibilityKeyMaterial.SetColor("_BaseColor", Color.magenta);
        else if (_visibilityKeyMaterial.HasProperty("_Color"))
            _visibilityKeyMaterial.SetColor("_Color", Color.magenta);
        if (_visibilityKeyMaterial.HasProperty("_Surface"))
            _visibilityKeyMaterial.SetFloat("_Surface", 0f);

        var cameraObject = new GameObject("AnimalVisibilityCamera");
        cameraObject.transform.SetParent(_camera.transform, false);

        _visibilityCamera = cameraObject.AddComponent<Camera>();
        _visibilityCamera.CopyFrom(_camera);
        _visibilityCamera.enabled = false;
        _visibilityCamera.targetTexture = _visibilityTexture;
        _visibilityCamera.clearFlags = CameraClearFlags.SolidColor;
        _visibilityCamera.backgroundColor = Color.black;
        _visibilityCamera.cullingMask = ~(1 << 5);
        _visibilityCamera.allowHDR = false;
        _visibilityCamera.allowMSAA = false;
        _visibilityCamera.useOcclusionCulling = false;
        _visibilityCamera.depthTextureMode = DepthTextureMode.None;

        UniversalAdditionalCameraData cameraData = _visibilityCamera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;

        if (!RenderPipeline.SupportsRenderRequest(_visibilityCamera, new UniversalRenderPipeline.SingleCameraRequest()))
        {
            Debug.LogError("[PlayerController] URP render requests are not supported. Animal visibility is disabled.");
            DisposeVisibilityResources();
            return;
        }

        _playerRenderers.Clear();
        _playerRenderers.AddRange(transform.root.GetComponentsInChildren<Renderer>());
    }

    private void DisposeVisibilityResources()
    {
        if (_visibilityCamera != null)
        {
            Destroy(_visibilityCamera.gameObject);
            _visibilityCamera = null;
        }

        if (_visibilityTexture != null)
        {
            _visibilityTexture.Release();
            Destroy(_visibilityTexture);
            _visibilityTexture = null;
        }

        if (_visibilityReadback != null)
        {
            Destroy(_visibilityReadback);
            _visibilityReadback = null;
        }

        if (_visibilityKeyMaterial != null)
        {
            Destroy(_visibilityKeyMaterial);
            _visibilityKeyMaterial = null;
        }
    }

    private IEnumerator VisibilityLoop()
    {
        var waitForEndOfFrame = new WaitForEndOfFrame();

        while (true)
        {
            yield return waitForEndOfFrame;

            if (_currentItem != ItemType.None)
            {
                _photoTarget = null;
                continue;
            }

            if (Time.unscaledTime < _nextVisibilityCheck)
                continue;

            _nextVisibilityCheck = Time.unscaledTime + visibilityCheckInterval;

            try
            {
                _photoTarget = FindVisibleAnimal();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[PlayerController] Visibility rendering failed. Animal visibility is disabled.");
                _photoTarget = null;
                DisposeVisibilityResources();
                yield break;
            }
        }
    }

    private Collider FindVisibleAnimal()
    {
        if (_visibilityCamera == null)
            return null;

        GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);

        foreach (Collider candidate in _candidates)
        {
            _candidateRenderers.Clear();
            candidate.GetComponentsInChildren(_candidateRenderers);

            if (!IsInsideFrustum())
                continue;

            if (IsRenderedVisible())
                return candidate;
        }

        return null;
    }

    private bool IsInsideFrustum()
    {
        bool hasBounds = false;
        Bounds bounds = default;

        foreach (Renderer renderer in _candidateRenderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds && GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds);
    }

    private bool IsRenderedVisible()
    {
        _keyedRenderers.Clear();
        _originalMaterials.Clear();

        foreach (Renderer renderer in _candidateRenderers)
        {
            if (renderer == null || !renderer.enabled || _keyedRenderers.Contains(renderer))
                continue;

            _keyedRenderers.Add(renderer);
            _originalMaterials.Add(renderer.sharedMaterials);
        }

        if (_keyedRenderers.Count == 0)
            return false;

        _visibilityCamera.aspect = _camera.aspect;
        _visibilityCamera.orthographic = _camera.orthographic;
        _visibilityCamera.orthographicSize = _camera.orthographicSize;
        _visibilityCamera.fieldOfView = _camera.fieldOfView;

        // The material swap and player hiding only live for this synchronous render request.
        try
        {
            for (int i = 0; i < _keyedRenderers.Count; i++)
            {
                int materialCount = Mathf.Max(1, _originalMaterials[i].Length);
                var keyedMaterials = new Material[materialCount];
                for (int j = 0; j < materialCount; j++)
                    keyedMaterials[j] = _visibilityKeyMaterial;
                _keyedRenderers[i].sharedMaterials = keyedMaterials;
            }

            if (ignorePlayer)
            {
                foreach (Renderer renderer in _playerRenderers)
                {
                    if (renderer == null || !renderer.enabled)
                        continue;
                    _hiddenPlayerRenderers.Add(renderer);
                    renderer.enabled = false;
                }
            }

            RenderPipeline.SubmitRenderRequest(_visibilityCamera,
                new UniversalRenderPipeline.SingleCameraRequest { destination = _visibilityTexture });
        }
        finally
        {
            for (int i = 0; i < _keyedRenderers.Count; i++)
                _keyedRenderers[i].sharedMaterials = _originalMaterials[i];

            foreach (Renderer renderer in _hiddenPlayerRenderers)
                renderer.enabled = true;
            _hiddenPlayerRenderers.Clear();
        }

        RenderTexture.active = _visibilityTexture;
        _visibilityReadback.ReadPixels(
            new Rect(0, 0, _visibilityTexture.width, _visibilityTexture.height), 0, 0, false);
        RenderTexture.active = null;

        var pixels = _visibilityReadback.GetRawTextureData<Color32>();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            if (pixel.r > 128 && pixel.b > 128 && pixel.g < 64)
                return true;
        }

        return false;
    }

    private void LogVisibilityDebug(int numOfColliders)
    {
        string message = _visibilityCamera == null
            ? "Animal visibility rendering is unavailable"
            : _currentItem != ItemType.None
                ? $"Holding {_currentItem}: camera stays away"
                : numOfColliders == 0
                    ? $"No Animal colliders within detectionDistance ({detectionDistance})"
                    : $"{numOfColliders} animal(s) in range but none produced visible pixels";

        if (message == _lastDebugMessage)
            return;

        _lastDebugMessage = message;
        Debug.Log($"[PlayerController] {message}");
    }
}
