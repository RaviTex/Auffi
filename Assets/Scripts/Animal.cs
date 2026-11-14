using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

[System.Serializable]
public class HasBeenPhotograph : UnityEvent
{
}

public class Animal : MonoBehaviour
{
    [SerializeField] private AnimalDefinition definition;

    [Header("Outline")]
    [Tooltip("Color of the outline shown while this animal can be photographed.")]
    [SerializeField] private Color outlineColor = Color.white;
    [Tooltip("Hull thickness in world units, independent of the animal's scale.")]
    [Range(0.001f, 0.2f)]
    [SerializeField] private float outlineWidth = 0.03f;

    [Header("Audio")] 
    [SerializeField] private AK.Wwise.Event playCowMooing;
    
    [Header("Fired when animal gets photographed")]
    [SerializeField] private HasBeenPhotograph hasBeenPhotograph = new HasBeenPhotograph();
    
    [Header("Photo Reaction")]
    [Tooltip("Sprite spawned above the animal after it has been photographed.")]
    [SerializeField] private Sprite heartSprite;
    [Tooltip("World-space height above the animal where the heart appears.")]
    [SerializeField] private float heartSpawnHeight = 2f;
    [SerializeField] private float heartRiseDistance = 1f;
    [SerializeField] private float heartDuration = 1.2f;
    [SerializeField] private float heartScale = 1f;

    [Header("Grazing")]
    [Tooltip("Chance that the animal starts grazing instead of idling.")]
    [Range(0f, 1f)]
    [SerializeField] private float grazingChance = 0.5f;
    [Tooltip("Minimum seconds between grazing and idle switches.")]
    [SerializeField] private float minGrazingSwitchTime = 10f;
    [Tooltip("Maximum seconds between grazing and idle switches.")]
    [SerializeField] private float maxGrazingSwitchTime = 30f;

    private static readonly int IsGrazingId = Animator.StringToHash("IsGrazing");
    private static readonly int IsWalkingId = Animator.StringToHash("IsWalking");

    public AnimalDefinition Definition => definition;
    public Color OutlineColor => outlineColor;
    public float OutlineWidth => outlineWidth;
    public bool HasBeenPhotographed { get; private set; }

    private float _mooingTimer;
    private float _timeToMoo;
    private Animator _animator;
    private bool _isGrazing;
    private float _grazingTimer;
    private float _timeToSwitchGrazing;
    private AnimalTravel _animalTravel;

    private void Start()
    {
        _timeToMoo = Random.Range(5, 30);

        _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
        {
            _isGrazing = Random.value < grazingChance;
            _animator.SetBool(IsGrazingId, _isGrazing);
        }

        _timeToSwitchGrazing = Random.Range(minGrazingSwitchTime, maxGrazingSwitchTime);
        _animalTravel = GetComponent<AnimalTravel>();
    }

    private void Update()
    {
        _mooingTimer += Time.deltaTime;
        if (_mooingTimer >= _timeToMoo)
        {
            playCowMooing?.Post(gameObject);
            _mooingTimer = 0f;
            _timeToMoo = Random.Range(15, 60);
        }

        if (_animator == null)
            return;

        _grazingTimer += Time.deltaTime;
        if (_grazingTimer > _timeToSwitchGrazing)
        {
            _isGrazing = !_isGrazing;
            _animator.SetBool(IsGrazingId, _isGrazing);

            _grazingTimer = 0f;
            _timeToSwitchGrazing = Random.Range(minGrazingSwitchTime, maxGrazingSwitchTime);
        }

        if (_animalTravel == null)
            return;
        _animator.SetBool(IsWalkingId, _animalTravel.IsTraveling);
        _animator.SetBool(IsGrazingId, !_animalTravel.IsTraveling && _isGrazing);
    }
    
    public void MarkPhotographed()
    {
        if (HasBeenPhotographed)
            return;

        HasBeenPhotographed = true;

        if (heartSprite != null)
            StartCoroutine(PlayHeartReaction());
        hasBeenPhotograph.Invoke();
    }

    private IEnumerator PlayHeartReaction()
    {
        Camera cam = Camera.main;
        GameObject heart = new GameObject("Photo Heart");
        SpriteRenderer spriteRenderer = heart.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = heartSprite;
        spriteRenderer.sortingOrder = 100;
        heart.transform.localScale = Vector3.one * heartScale;

        Vector3 anchor = transform.position + Vector3.up * heartSpawnHeight;
        Vector3 pivotOffset = heartSprite.bounds.center * heartScale;
        Quaternion rotation = cam != null ? cam.transform.rotation : Quaternion.identity;

        float elapsed = 0f;
        while (elapsed < heartDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / heartDuration);

            if (cam != null)
                rotation = cam.transform.rotation;

            heart.transform.SetPositionAndRotation(
                anchor + Vector3.up * (heartRiseDistance * t) - rotation * pivotOffset,
                rotation);

            Color color = spriteRenderer.color;
            color.a = 1f - t;
            spriteRenderer.color = color;

            yield return null;
        }

        Destroy(heart);
    }
    
}
