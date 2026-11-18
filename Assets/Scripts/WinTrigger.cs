using UnityEngine;

public class WinTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null)
            return;

        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.Win();
    }
}
