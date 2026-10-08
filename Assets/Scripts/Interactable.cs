using UnityEngine;

// Needs a Collider with "Is Trigger" enabled on this GameObject.
public abstract class Interactable : MonoBehaviour
{
    [Tooltip("Point the player faces while this is the nearest interactable. Defaults to this transform.")]
    [SerializeField] private Transform focusPoint;

    public Vector3 FocusPosition => focusPoint != null ? focusPoint.position : transform.position;

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
            player.RegisterInteractable(this);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
            player.UnregisterInteractable(this);
    }

    public virtual void OnFocus(PlayerController player)
    {
    }

    public virtual void OnUnfocus(PlayerController player)
    {
    }

    public abstract void Interact(PlayerController player);
}
