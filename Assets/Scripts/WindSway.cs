using UnityEngine;

// Marks a plant as wind-animated and widens its culling bounds so the shader
// displacement never pops when the undisplaced mesh leaves the camera frustum.
[DisallowMultipleComponent]
public class WindSway : MonoBehaviour
{
    [Tooltip("Extra world-space margin added to the renderer bounds.")]
    [SerializeField, Min(0f)] private float boundsMargin = 1f;

    private void Awake()
    {
        foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
        {
            Vector3 scale = target.transform.lossyScale;
            Vector3 margin = new Vector3(
                boundsMargin / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                boundsMargin / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
                boundsMargin / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));

            Bounds bounds = target.localBounds;
            bounds.extents += margin;
            target.localBounds = bounds;
        }
    }
}
