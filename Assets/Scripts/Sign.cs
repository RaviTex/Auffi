using UnityEngine;

// A world sign that can be read by interacting with it.
public class Sign : Interactable
{
    [TextArea(2, 10)]
    [Tooltip("Text shown in the sign UI when the player interacts with this sign.")]
    [SerializeField] private string message;

    public override void Interact(PlayerController player)
    {
        if (UIManager.Instance == null)
        {
            Debug.LogError("[Sign] No UIManager was found in the scene. Sign text cannot be shown.", this);
            return;
        }

        UIManager.Instance.ShowSign(message);
    }
}
