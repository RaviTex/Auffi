using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject gameOverScreen;
    [SerializeField] private GameObject bookUI;

    public void GameOver()
    {
        StartCoroutine(GameOverCoroutine());
    }

    private IEnumerator GameOverCoroutine()
    {
        bookUI.SetActive(false);
        gameOverScreen.SetActive(true);
        Time.timeScale = 0;

        // Realtime: scaled time is frozen above.
        yield return new WaitForSecondsRealtime(3);

        Time.timeScale = 1;
        SceneManager.LoadScene(0);
    }
}
