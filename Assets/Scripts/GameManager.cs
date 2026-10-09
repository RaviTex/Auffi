using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private PlayerController player;
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
        print("Game Over");
        yield return new WaitForSeconds(3);
        SceneManager.LoadScene(0);
    }
}
