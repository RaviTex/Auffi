using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

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

    [Header("Photo Reaction")]
    [Tooltip("Sprite spawned above the animal after it has been photographed.")]
    [SerializeField] private Sprite heartSprite;
    [Tooltip("World-space height above the animal where the heart appears.")]
    [SerializeField] private float heartSpawnHeight = 2f;
    [SerializeField] private float heartRiseDistance = 1f;
    [SerializeField] private float heartDuration = 1.2f;
    [SerializeField] private float heartScale = 1f;

    public AnimalDefinition Definition => definition;
    public Color OutlineColor => outlineColor;
    public float OutlineWidth => outlineWidth;
    public bool HasBeenPhotographed { get; private set; }

    private float _mooingTimer;
    private float _timeToMoo;

    public void MarkPhotographed()
    {
        if (HasBeenPhotographed)
            return;

        HasBeenPhotographed = true;

        if (heartSprite != null)
            StartCoroutine(PlayHeartReaction());
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

    private void Start()
    {
        _timeToMoo = Random.Range(5, 30);
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
    }
}
