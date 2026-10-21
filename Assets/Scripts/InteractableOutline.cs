using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Highlights the focused interactable, or the animal that can currently be photographed,
// with an inverted hull outline: every mesh of the object is redrawn pushed out along its
// normals with front faces culled, so the enlarged back faces read as an outline.
public class InteractableOutline : MonoBehaviour
{
    private const string ShaderName = "Auffi/Interactable Outline";

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [Tooltip("Outline shader. Falls back to Shader.Find when left empty.")]
    [SerializeField] private Shader outlineShader;
    [Tooltip("Default hull thickness in world units. Animals can override it per instance.")]
    [Range(0.001f, 0.2f)]
    [SerializeField] private float outlineWidth = 0.03f;
    [Tooltip("Default outline color. Animals can override it per instance.")]
    [SerializeField] private Color outlineColor = Color.white;

    private Material _material;
    private Camera _camera;
    private Transform _targetRoot;
    private Color _activeOutlineColor;
    private float _activeOutlineWidth;

    private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();

    private void Awake()
    {
        Shader shader = outlineShader != null ? outlineShader : Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("[InteractableOutline] Outline shader not found. Outlines are disabled.");
            enabled = false;
            return;
        }

        _material = new Material(shader) { name = "InteractableOutline (Runtime)" };
        _activeOutlineColor = outlineColor;
        _activeOutlineWidth = outlineWidth;
        ApplyProperties();
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
    }

    public void SetTarget(Interactable target)
    {
        if (target == null)
            SetTarget(null, outlineColor, outlineWidth);
        else
            SetTarget(target.OutlineRoot, outlineColor, outlineWidth);
    }

    // Animals are photographed rather than triggered, so the player supplies them explicitly
    // and each animal can override the outline style configured on this component.
    public void SetTarget(Animal target)
    {
        if (target == null)
            SetTarget(null, outlineColor, outlineWidth);
        else
            SetTarget(target.transform, target.OutlineColor, target.OutlineWidth);
    }

    private void SetTarget(Transform targetRoot, Color color, float width)
    {
        if (_targetRoot == targetRoot && _activeOutlineColor == color &&
            Mathf.Approximately(_activeOutlineWidth, width))
            return;

        _targetRoot = targetRoot;
        _activeOutlineColor = color;
        _activeOutlineWidth = width;
        ApplyProperties();
    }

    private void LateUpdate()
    {
        if (_material == null || _targetRoot == null)
            return;

        if (_camera == null)
            _camera = Camera.main;

        _renderers.Clear();
        _targetRoot.GetComponentsInChildren(_renderers);

        foreach (MeshRenderer renderer in _renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.isVisible)
                continue;

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
                continue;

            Bounds bounds = renderer.bounds;
            bounds.Expand(_activeOutlineWidth * 2f);

            var parameters = new RenderParams(_material)
            {
                camera = _camera,
                layer = renderer.gameObject.layer,
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                lightProbeUsage = LightProbeUsage.Off,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
            };

            Matrix4x4 matrix = renderer.localToWorldMatrix;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                Graphics.RenderMesh(parameters, mesh, submesh, matrix);
        }
    }

    private void ApplyProperties()
    {
        if (_material == null)
            return;

        _material.SetColor(OutlineColorId, _activeOutlineColor);
        _material.SetFloat(OutlineWidthId, _activeOutlineWidth);
    }
}
