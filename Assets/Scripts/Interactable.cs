using UnityEngine;

// The interaction range is defined by a Collider with "Is Trigger" enabled on this GameObject.
public abstract class Interactable : MonoBehaviour
{
    [Tooltip("Point the player faces while this is the nearest interactable. Defaults to this transform.")]
    [SerializeField] private Transform focusPoint;
    [Tooltip("Object that gets the interaction outline. Use this when the visible object is not a child of the trigger. Defaults to this transform.")]
    [SerializeField] private Transform outlineRoot;

    public Vector3 FocusPosition => focusPoint != null ? focusPoint.position : transform.position;
    public Transform OutlineRoot => outlineRoot != null ? outlineRoot : transform;

    public virtual void OnFocus(PlayerController player)
    {
    }

    public virtual void OnUnfocus(PlayerController player)
    {
    }

    public abstract void Interact(PlayerController player);
}
