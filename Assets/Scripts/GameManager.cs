using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Tooltip("How long the Game Over screen stays up before the scene reloads.")]
    [SerializeField] private float gameOverDuration = 3f;

    private bool _isGameOver;

    public void GameOver()
    {
        if (_isGameOver)
            return;

        _isGameOver = true;
        StartCoroutine(GameOverCoroutine());
    }

    private IEnumerator GameOverCoroutine()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGameOver();
        }
        else
        {
            Debug.LogError("[GameManager] No UIManager was found in the scene. Freezing without a Game Over screen.");
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        yield return new WaitForSecondsRealtime(gameOverDuration);

        if (UIManager.Instance != null)
            UIManager.Instance.ResetForSceneLoad();
        else
            Time.timeScale = 1f;

        SceneManager.LoadScene(0);
    }
}
