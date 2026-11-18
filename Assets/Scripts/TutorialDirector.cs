using UnityEngine;

public class TutorialDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private AnimalVisibilitySampler visibility;
    [SerializeField] private AnimalBook book;
    [SerializeField] private PhotoProgression progression;
    [SerializeField] private Animal startingCow;
    [SerializeField] private Animal hiddenCow;
    [SerializeField] private Animal bull;
    [SerializeField] private Transform trough;
    [SerializeField] private PutDownPivot troughPivot;
    [SerializeField] private AnimalTravel brownTravel;
    [SerializeField] private AnimalTravel hornedTravel;

    [Header("Dialogues")]
    [SerializeField] private DialogueSequence introDialogue;
    [SerializeField] private DialogueSequence introAfterBookDialogue;
    [SerializeField] private DialogueSequence firstCowIntroDialogue;
    [SerializeField] private DialogueSequence firstCowPhotoDialogue;
    [SerializeField] private DialogueSequence troughDialogue;
    [SerializeField] private DialogueSequence hiddenCowDialogue;
    [SerializeField] private DialogueSequence bullDialogue;

    [Header("Tuning")]
    [SerializeField] private float introDelay = 0.75f;
    [SerializeField] private float firstCowRadius = 11f;
    [SerializeField] private float troughRadius = 7f;
    [SerializeField] private float hiddenCowRadius = 9f;
    [SerializeField] private float bullRadius = 9f;
    [SerializeField, Range(0.1f, 1f)] private float triggerRadiusScale = 0.85f;

    private UIManager _ui;
    private float _startTime;
    private string _activeDialogue;
    private int _photosTaken;
    private bool _introComplete;
    private bool _firstCowIntroComplete;
    private bool _firstCowPhotoComplete;
    private bool _troughHintComplete;
    private bool _troughSolved;
    private bool _hiddenCowComplete;
    private bool _bullComplete;
    private bool _waitingForBook;
    private bool _bookWasOpened;

    private void Start()
    {
        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null && !gameManager.TutorialEnabled)
        {
            enabled = false;
            return;
        }

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (visibility == null && player != null)
            visibility = player.GetComponent<AnimalVisibilitySampler>();

        if (book == null)
            book = FindFirstObjectByType<AnimalBook>();

        if (progression == null)
            progression = FindFirstObjectByType<PhotoProgression>();

        _ui = UIManager.Instance;
        _startTime = Time.unscaledTime;

        if (book != null)
            book.AnimalUnlocked += OnAnimalUnlocked;

        if (troughPivot != null)
        {
            Transform applesVisual = troughPivot.transform.Find("Apples");
            troughPivot.ConfigureAcceptedItems(
                new[] {ItemType.Apples},
                applesVisual != null ? applesVisual.gameObject : null,
                true);

            troughPivot.ItemPutDown += OnItemPutDown;
        }
    }

    private void OnDestroy()
    {
        if (book != null)
            book.AnimalUnlocked -= OnAnimalUnlocked;

        if (troughPivot != null)
            troughPivot.ItemPutDown -= OnItemPutDown;
    }

    private void Update()
    {
        if (_ui == null)
            _ui = UIManager.Instance;

        if (_ui == null)
            return;

        if (_activeDialogue != null)
        {
            if (!_ui.IsDialogueOpen)
                CompleteActiveDialogue();

            return;
        }

        if (_waitingForBook)
        {
            if (_ui.IsBookOpen)
            {
                _bookWasOpened = true;
            }
            else if (_bookWasOpened)
            {
                if (introAfterBookDialogue == null)
                {
                    _waitingForBook = false;
                    _introComplete = true;
                }
                else
                {
                    Begin("intro_after_book", introAfterBookDialogue);

                    if (_activeDialogue != null)
                        _waitingForBook = false;
                }
            }

            return;
        }

        if (_ui.IsBlockingGameplay)
            return;

        if (!_introComplete)
        {
            if (Time.unscaledTime - _startTime >= introDelay)
                BeginIntro();

            return;
        }

        if (!_firstCowIntroComplete)
        {
            if (startingCow == null || startingCow.HasBeenPhotographed)
            {
                _firstCowIntroComplete = true;
                _firstCowPhotoComplete = true;
            }
            else if (IsNear(startingCow.transform, firstCowRadius))
            {
                Begin("first_cow_intro", firstCowIntroDialogue);
            }

            return;
        }

        if (!_firstCowPhotoComplete)
        {
            if (startingCow == null || startingCow.HasBeenPhotographed)
            {
                _firstCowPhotoComplete = true;
            }
            else if (visibility != null && visibility.VisibleAnimal == startingCow)
            {
                Begin("first_cow_photo", firstCowPhotoDialogue);
            }

            return;
        }

        if (!_troughHintComplete && !_troughSolved && _photosTaken > 0 &&
            player != null && !player.HasApples && trough != null && IsNear(trough, troughRadius))
        {
            Begin("trough", troughDialogue);
            return;
        }

        if (!_hiddenCowComplete && _photosTaken > 0 && hiddenCow != null && !hiddenCow.HasBeenPhotographed)
        {
            bool isHidden = visibility == null || visibility.VisibleAnimal != hiddenCow;

            if (isHidden && IsNear(hiddenCow.transform, hiddenCowRadius))
            {
                Begin("hidden_cow", hiddenCowDialogue);
                return;
            }
        }

        if (!_bullComplete && progression != null && progression.HasRevealed &&
            bull != null && !bull.HasBeenPhotographed && IsNear(bull.transform, bullRadius))
            Begin("bull", bullDialogue);
    }

    private void BeginIntro()
    {
        if (introDialogue == null)
        {
            _introComplete = true;
            return;
        }

        Begin("intro", introDialogue);
    }

    private void Begin(string id, DialogueSequence sequence)
    {
        if (sequence == null || _ui == null || _ui.IsBlockingGameplay)
            return;

        _ui.ShowDialogue(sequence);

        if (_ui.IsDialogueOpen)
            _activeDialogue = id;
    }

    private void CompleteActiveDialogue()
    {
        string completed = _activeDialogue;
        _activeDialogue = null;

        switch (completed)
        {
            case "intro":
                _waitingForBook = true;
                _bookWasOpened = false;
                break;
            case "intro_after_book":
                _introComplete = true;
                break;
            case "first_cow_intro":
                _firstCowIntroComplete = true;
                break;
            case "first_cow_photo":
                _firstCowPhotoComplete = true;
                break;
            case "trough":
                _troughHintComplete = true;
                break;
            case "hidden_cow":
                _hiddenCowComplete = true;
                break;
            case "bull":
                _bullComplete = true;
                break;
        }
    }

    private void OnAnimalUnlocked(AnimalDefinition definition)
    {
        _photosTaken++;
    }

    private void OnItemPutDown(ItemType item)
    {
        if (item != ItemType.Apples)
            return;

        _troughSolved = true;
        _troughHintComplete = true;

        if (brownTravel != null)
            brownTravel.TravelToDestination();

        if (hornedTravel != null)
            hornedTravel.TravelToDestination();
    }

    private bool IsNear(Transform target, float radius)
    {
        if (target == null || player == null)
            return false;

        float scaledRadius = radius * triggerRadiusScale;
        return Vector3.SqrMagnitude(target.position - player.transform.position) <= scaledRadius * scaledRadius;
    }
}
