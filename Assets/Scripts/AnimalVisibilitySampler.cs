using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Finds which nearby animal is visible to the camera by rendering the scene with the animal
// drawn in a key color and scanning the result. The scene's depth testing handles occlusion
// with the real meshes, so a partially occluded animal still counts as visible.
public class AnimalVisibilitySampler : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Radius around the player in which animals are checked.")]
    [SerializeField] private float detectionDistance = 4f;
    [Tooltip("How often animal visibility is re-checked, in seconds.")]
    [SerializeField] private float checkInterval = 0.1f;
    [Tooltip("How long an animal stays visible after it was last seen. Prevents flickering between checks.")]
    [SerializeField] private float graceTime = 0.15f;

    [Header("Rendering")]
    [Tooltip("Height in pixels of the internal visibility render. Width follows the game's aspect ratio.")]
    [SerializeField] private int textureHeight = 90;
    [Tooltip("Excludes the player's layer from the visibility render, so the player never blocks the view of an animal.")]
    [SerializeField] private bool ignorePlayer = true;
    [SerializeField] private bool debugVisibility;

    private const int MaxAnimalsDetected = 16;
    private const int UiLayer = 5;
    private const int PlayerLayer = 8;

    private Camera _camera;
    private Camera _visibilityCamera;
    private RenderTexture _visibilityTexture;
    private Material _keyMaterial;
    private Coroutine _checkLoop;
    private LayerMask _animalMask;
    private Comparison<Candidate> _bestFirst;
    private float _nextCheckTime;
    private float _lastSeenTime = float.NegativeInfinity;
    private string _lastDebugMessage;

    private bool _readbackPending;
    private Animal _pendingAnimal;
    private Vector3 _pendingCenter;

    private readonly Collider[] _animalsInRange = new Collider[MaxAnimalsDetected];
    private readonly List<Candidate> _candidates = new List<Candidate>();
    private readonly Plane[] _frustumPlanes = new Plane[6];
    private readonly List<Renderer> _targetRenderers = new List<Renderer>();
    private readonly List<Renderer> _keyedRenderers = new List<Renderer>();
    private readonly List<Material[]> _originalMaterials = new List<Material[]>();

    // Set while the player holds an item, when sampling would be pointless.
    public bool Paused { get; set; }

    public Animal VisibleAnimal { get; private set; }
    public Vector3 VisibleAnimalCenter { get; private set; }

    public bool TryGetVisibleAnimal(out Animal animal, out Vector3 center)
    {
        bool visible = !Paused && VisibleAnimal != null && Time.time - _lastSeenTime <= graceTime;
        animal = visible ? VisibleAnimal : null;
        center = visible ? VisibleAnimalCenter : Vector3.zero;
        return visible;
    }

    private void Start()
    {
        _camera = Camera.main;
        _animalMask = LayerMask.GetMask("Animal");
        _bestFirst = CompareByPreference;

        if (CreateVisibilityCamera())
            _checkLoop = StartCoroutine(CheckForVisibleAnimals());
    }

    private void OnDestroy()
    {
        if (_checkLoop != null)
            StopCoroutine(_checkLoop);

        DisposeResources();
    }

    private IEnumerator CheckForVisibleAnimals()
    {
        var waitForEndOfFrame = new WaitForEndOfFrame();

        while (true)
        {
            yield return waitForEndOfFrame;

            if (Paused)
            {
                SetVisibleAnimal(null, default);

                if (debugVisibility)
                    LogVisibilityDebug();

                continue;
            }

            if (Time.unscaledTime < _nextCheckTime || _readbackPending)
                continue;

            _nextCheckTime = Time.unscaledTime + checkInterval;

            try
            {
                if (RenderAnimalForCheck(out _pendingAnimal, out _pendingCenter))
                {
                    _readbackPending = true;
                    AsyncGPUReadback.Request(_visibilityTexture, 0, OnReadbackComplete);
                }
                else
                {
                    SetVisibleAnimal(null, default);

                    if (debugVisibility)
                        LogVisibilityDebug();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[AnimalVisibilitySampler] Visibility rendering failed. Animal visibility is disabled.");
                SetVisibleAnimal(null, default);
                StopLoopAndDispose();
                yield break;
            }
        }
    }

    // Renders the animal that best matches the current view, with every renderer keyed, so no
    // part of it can occlude another and hide the whole animal.
    private bool RenderAnimalForCheck(out Animal animal, out Vector3 center)
    {
        animal = null;
        center = default;

        if (_visibilityCamera == null)
            return false;

        int count = Physics.OverlapSphereNonAlloc(transform.position, detectionDistance, _animalsInRange,
            _animalMask);
        GatherCandidates(count);

        GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);

        foreach (Candidate candidate in _candidates)
        {
            _targetRenderers.Clear();
            candidate.Animal.GetComponentsInChildren(_targetRenderers);

            if (!IsInsideFrustum(out Bounds bounds))
                continue;

            // The key materials only live for this synchronous render request.
            try
            {
                if (!ApplyKeyMaterials())
                    continue;

                SubmitRender();
            }
            finally
            {
                RestoreKeyMaterials();
            }

            animal = candidate.Animal;
            center = bounds.center;
            return true;
        }

        return false;
    }

    // One entry per animal, the one closest to the center of the view first. The overlap order
    // is unspecified and an animal has several colliders, so this keeps the result stable.
    private void GatherCandidates(int count)
    {
        _candidates.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider collider = _animalsInRange[i];
            if (collider == null)
                continue;

            Animal animal = collider.GetComponentInParent<Animal>();
            if (animal == null)
                continue;

            Vector3 center = collider.bounds.center;
            var candidate = new Candidate
            {
                Animal = animal,
                SqrDistance = (center - transform.position).sqrMagnitude,
                ViewAngle = Vector3.Angle(_camera.transform.forward, center - _camera.transform.position)
            };

            int index = IndexOf(animal);
            if (index < 0)
                _candidates.Add(candidate);
            else if (candidate.SqrDistance < _candidates[index].SqrDistance)
                _candidates[index] = candidate;
        }

        _candidates.Sort(_bestFirst);
    }

    private int IndexOf(Animal animal)
    {
        for (int i = 0; i < _candidates.Count; i++)
        {
            if (_candidates[i].Animal == animal)
                return i;
        }

        return -1;
    }

    private static int CompareByPreference(Candidate a, Candidate b)
    {
        int byAngle = a.ViewAngle.CompareTo(b.ViewAngle);
        return byAngle != 0 ? byAngle : a.SqrDistance.CompareTo(b.SqrDistance);
    }

    private bool IsInsideFrustum(out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = default;

        foreach (Renderer renderer in _targetRenderers)
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

    private bool ApplyKeyMaterials()
    {
        _keyedRenderers.Clear();
        _originalMaterials.Clear();

        foreach (Renderer renderer in _targetRenderers)
        {
            if (renderer == null || !renderer.enabled || _keyedRenderers.Contains(renderer))
                continue;

            _keyedRenderers.Add(renderer);
            _originalMaterials.Add(renderer.sharedMaterials);
        }

        for (int i = 0; i < _keyedRenderers.Count; i++)
        {
            int materialCount = Mathf.Max(1, _originalMaterials[i].Length);
            var keyedMaterials = new Material[materialCount];

            for (int j = 0; j < materialCount; j++)
                keyedMaterials[j] = _keyMaterial;

            _keyedRenderers[i].sharedMaterials = keyedMaterials;
        }

        return _keyedRenderers.Count > 0;
    }

    private void RestoreKeyMaterials()
    {
        for (int i = 0; i < _keyedRenderers.Count; i++)
            _keyedRenderers[i].sharedMaterials = _originalMaterials[i];
    }

    private void SubmitRender()
    {
        _visibilityCamera.aspect = _camera.aspect;
        _visibilityCamera.orthographic = _camera.orthographic;
        _visibilityCamera.orthographicSize = _camera.orthographicSize;
        _visibilityCamera.fieldOfView = _camera.fieldOfView;

        RenderPipeline.SubmitRenderRequest(_visibilityCamera,
            new UniversalRenderPipeline.SingleCameraRequest { destination = _visibilityTexture });
    }

    private void OnReadbackComplete(AsyncGPUReadbackRequest request)
    {
        _readbackPending = false;

        if (_visibilityTexture == null)
            return;

        if (request.hasError)
        {
            Debug.LogError("[AnimalVisibilitySampler] GPU readback failed. Animal visibility is disabled.");
            SetVisibleAnimal(null, default);
            StopLoopAndDispose();
            return;
        }

        if (!Paused && ContainsKeyPixel(request.GetData<Color32>()))
            SetVisibleAnimal(_pendingAnimal, _pendingCenter);
        else
            SetVisibleAnimal(null, default);
    }

    private static bool ContainsKeyPixel(NativeArray<Color32> pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            if (pixel.r > 128 && pixel.b > 128 && pixel.g < 64)
                return true;
        }

        return false;
    }

    private void SetVisibleAnimal(Animal animal, Vector3 center)
    {
        VisibleAnimal = animal;
        VisibleAnimalCenter = center;

        if (animal != null)
            _lastSeenTime = Time.time;
    }

    private bool CreateVisibilityCamera()
    {
        if (_camera == null)
        {
            Debug.LogError("[AnimalVisibilitySampler] No main camera found. Animal visibility is disabled.");
            return false;
        }

        Shader keyShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (keyShader == null)
        {
            Debug.LogError("[AnimalVisibilitySampler] URP unlit shader not found. Animal visibility is disabled.");
            return false;
        }

        int height = Mathf.Max(16, textureHeight);
        int width = Mathf.Max(16, Mathf.RoundToInt(height * _camera.aspect));

        _visibilityTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "AnimalVisibility",
            filterMode = FilterMode.Point,
            antiAliasing = 1,
            useMipMap = false
        };
        _visibilityTexture.Create();

        _keyMaterial = new Material(keyShader)
        {
            name = "AnimalVisibilityKey",
            renderQueue = 2450
        };
        if (_keyMaterial.HasProperty("_BaseColor"))
            _keyMaterial.SetColor("_BaseColor", Color.magenta);
        else if (_keyMaterial.HasProperty("_Color"))
            _keyMaterial.SetColor("_Color", Color.magenta);
        if (_keyMaterial.HasProperty("_Surface"))
            _keyMaterial.SetFloat("_Surface", 0f);

        var cameraObject = new GameObject("AnimalVisibilityCamera");
        cameraObject.transform.SetParent(_camera.transform, false);

        _visibilityCamera = cameraObject.AddComponent<Camera>();
        _visibilityCamera.CopyFrom(_camera);
        _visibilityCamera.enabled = false;
        _visibilityCamera.targetTexture = _visibilityTexture;
        _visibilityCamera.clearFlags = CameraClearFlags.SolidColor;
        _visibilityCamera.backgroundColor = Color.black;
        // Skip the UI layer, and the player's own layer so the player never blocks an animal.
        int excludedLayers = (1 << UiLayer) | (ignorePlayer ? 1 << PlayerLayer : 0);
        _visibilityCamera.cullingMask = ~excludedLayers;
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
            Debug.LogError("[AnimalVisibilitySampler] URP render requests are not supported. Animal visibility is disabled.");
            DisposeResources();
            return false;
        }

        return true;
    }

    private void StopLoopAndDispose()
    {
        DisposeResources();

        if (_checkLoop != null)
        {
            StopCoroutine(_checkLoop);
            _checkLoop = null;
        }
    }

    private void DisposeResources()
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

        if (_keyMaterial != null)
        {
            Destroy(_keyMaterial);
            _keyMaterial = null;
        }
    }

    private void LogVisibilityDebug()
    {
        string message = _visibilityCamera == null
            ? "Animal visibility rendering is unavailable"
            : Paused
                ? "Holding an item: camera stays away"
                : _candidates.Count == 0
                    ? $"No animals within detectionDistance ({detectionDistance})"
                    : $"{_candidates.Count} animal(s) in range but none produced visible pixels";

        if (message == _lastDebugMessage)
            return;

        _lastDebugMessage = message;
        Debug.Log($"[AnimalVisibilitySampler] {message}");
    }

    private struct Candidate
    {
        public Animal Animal;
        public float SqrDistance;
        public float ViewAngle;
    }
}
