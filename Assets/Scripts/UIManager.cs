using TMPro;
using UnityEngine;

// Owns every modal UI overlay (book, sign, game over) and the gameplay freeze that comes
// with it. Only one modal can be open at a time.
public class UIManager : MonoBehaviour
{
    private enum Mode
    {
        None,
        Book,
        Sign,
        GameOver
    }

    public static UIManager Instance { get; private set; }

    [Header("Panels")]
    [Tooltip("Animal book controller. Defaults to the AnimalBook on this GameObject.")]
    [SerializeField] private AnimalBook book;
    [Tooltip("Panel shown while a sign is being read. It is hidden on Awake.")]
    [SerializeField] private GameObject signPanel;
    [Tooltip("Text component inside the sign panel.")]
    [SerializeField] private TMP_Text signText;
    [Tooltip("Panel shown when the game is over. It is hidden on Awake.")]
    [SerializeField] private GameObject gameOverScreen;

    private Mode _mode;
    private bool _hasSavedGameplayState;
    private float _savedTimeScale;
    private bool _savedCursorVisible;
    private CursorLockMode _savedCursorLock;

    public bool IsBlockingGameplay => _mode != Mode.None;
    public bool IsSignOpen => _mode == Mode.Sign;
    public bool IsBookOpen => _mode == Mode.Book;
    public bool IsGameOver => _mode == Mode.GameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[UIManager] More than one UIManager is active. Destroying the duplicate.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (book == null)
            book = GetComponentInChildren<AnimalBook>(true);

        if (signText == null && signPanel != null)
            signText = signPanel.GetComponentInChildren<TMP_Text>(true);

        if (book != null)
            book.Close();

        if (signPanel != null)
            signPanel.SetActive(false);

        if (gameOverScreen != null)
            gameOverScreen.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        RestoreGameplayState();
        Instance = null;
    }

    public void OpenBook()
    {
        if (_mode != Mode.None)
            return;

        if (book == null)
        {
            Debug.LogWarning("[UIManager] No AnimalBook is assigned. The book cannot be opened.", this);
            return;
        }

        book.Open();
        SetMode(Mode.Book);
    }

    public void CloseBook()
    {
        if (_mode != Mode.Book)
            return;

        if (book != null)
            book.Close();

        SetMode(Mode.None);
    }

    public void ToggleBook()
    {
        if (_mode == Mode.Book)
            CloseBook();
        else
            OpenBook();
    }

    public void ShowSign(string text)
    {
        if (_mode != Mode.None)
            return;

        if (signPanel == null || signText == null)
        {
            Debug.LogWarning("[UIManager] Sign panel or sign text is not assigned. Sign UI cannot be shown.", this);
            return;
        }

        signText.text = text;
        signPanel.SetActive(true);
        SetMode(Mode.Sign);
    }

    public void CloseSign()
    {
        if (_mode != Mode.Sign)
            return;

        if (signPanel != null)
            signPanel.SetActive(false);

        SetMode(Mode.None);
    }

    public void ShowGameOver()
    {
        if (_mode == Mode.GameOver)
            return;

        if (book != null)
            book.Close();

        if (signPanel != null)
            signPanel.SetActive(false);

        if (gameOverScreen != null)
            gameOverScreen.SetActive(true);
        else
            Debug.LogWarning("[UIManager] No Game Over screen is assigned.", this);

        SetMode(Mode.GameOver);
    }

    // Restores the gameplay state saved when the first modal opened. Call this before
    // reloading a scene so time and cursor state do not leak into the next scene.
    public void ResetForSceneLoad()
    {
        if (book != null)
            book.Close();

        if (signPanel != null)
            signPanel.SetActive(false);

        if (gameOverScreen != null)
            gameOverScreen.SetActive(false);

        SetMode(Mode.None);
    }

    private void SetMode(Mode mode)
    {
        _mode = mode;

        if (mode == Mode.None)
            RestoreGameplayState();
        else
            FreezeGameplay();
    }

    private void FreezeGameplay()
    {
        if (_hasSavedGameplayState)
            return;

        _savedTimeScale = Time.timeScale;
        _savedCursorVisible = Cursor.visible;
        _savedCursorLock = Cursor.lockState;
        _hasSavedGameplayState = true;

        // Time scale zero stops movement, normal Animator updates and AnimalTravel.
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void RestoreGameplayState()
    {
        if (!_hasSavedGameplayState)
            return;

        Time.timeScale = _savedTimeScale;
        Cursor.visible = _savedCursorVisible;
        Cursor.lockState = _savedCursorLock;
        _hasSavedGameplayState = false;
    }
}
