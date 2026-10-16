using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Highlights the focused interactable with an inverted hull outline: every mesh of the
// object is redrawn pushed out along its normals with front faces culled, so the enlarged
// back faces read as an outline around the object.
public class InteractableOutline : MonoBehaviour
{
    private const string ShaderName = "Auffi/Interactable Outline";

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [Tooltip("Outline shader. Falls back to Shader.Find when left empty.")]
    [SerializeField] private Shader outlineShader;
    [Tooltip("Hull thickness in world units, independent of the object's scale.")]
    [Range(0.001f, 0.2f)]
    [SerializeField] private float outlineWidth = 0.03f;
    [SerializeField] private Color outlineColor = Color.white;

    private Material _material;
    private Camera _camera;
    private Interactable _target;

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
        ApplyProperties();
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
    }

    public void SetTarget(Interactable target)
    {
        _target = target;
    }

    private void LateUpdate()
    {
        if (_material == null || _target == null)
            return;

        if (_camera == null)
            _camera = Camera.main;

        _renderers.Clear();
        _target.OutlineRoot.GetComponentsInChildren(_renderers);

        foreach (MeshRenderer renderer in _renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.isVisible)
                continue;

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
                continue;

            Bounds bounds = renderer.bounds;
            bounds.Expand(outlineWidth * 2f);

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
        _material.SetColor(OutlineColorId, outlineColor);
        _material.SetFloat(OutlineWidthId, outlineWidth);
    }
}
