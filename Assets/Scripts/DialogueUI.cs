using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private RawImage portraitImage;
    [SerializeField] private GameObject continueIndicator;
    [SerializeField] private float charactersPerSecond = 45f;

    private DialogueSequence _sequence;
    private Coroutine _typingCoroutine;
    private int _lineIndex;
    private int _visibleCharacters;
    private float _characterProgress;
    private float _openedAt;
    private bool _isTyping;

    public bool IsOpen => panel != null && panel.activeSelf;
    public event Action Closed;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        if (continueIndicator != null)
            continueIndicator.SetActive(false);
    }

    public void Show(DialogueSequence sequence)
    {
        if (sequence == null || sequence.Lines == null || sequence.Lines.Length == 0)
            return;

        _sequence = sequence;
        _lineIndex = 0;
        _openedAt = Time.unscaledTime;
        panel.SetActive(true);

        if (speakerText != null)
            speakerText.text = sequence.SpeakerName;

        if (portraitImage != null)
        {
            portraitImage.texture = sequence.Portrait;
            portraitImage.enabled = sequence.Portrait != null;
        }

        ShowLine();
    }

    public void Advance()
    {
        if (!IsOpen || _sequence == null || Time.unscaledTime - _openedAt < 0.15f)
            return;

        if (_isTyping)
        {
            RevealAll();
            return;
        }

        if (_lineIndex + 1 < _sequence.Lines.Length)
        {
            _lineIndex++;
            ShowLine();
            return;
        }

        Close();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        StopTyping();

        if (panel != null)
            panel.SetActive(false);

        _sequence = null;
        Closed?.Invoke();
    }

    private void ShowLine()
    {
        StopTyping();

        if (bodyText == null)
            return;

        bodyText.text = _sequence.Lines[_lineIndex];
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();

        _visibleCharacters = 0;
        _characterProgress = 0f;
        _isTyping = true;

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        _typingCoroutine = StartCoroutine(TypeLine());
    }

    private IEnumerator TypeLine()
    {
        int totalCharacters = bodyText.textInfo.characterCount;

        while (_visibleCharacters < totalCharacters)
        {
            _characterProgress += charactersPerSecond * Time.unscaledDeltaTime;
            int target = Mathf.Min(totalCharacters, Mathf.FloorToInt(_characterProgress));

            if (target != _visibleCharacters)
            {
                _visibleCharacters = target;
                bodyText.maxVisibleCharacters = _visibleCharacters;
            }

            yield return null;
        }

        _typingCoroutine = null;
        FinishTyping();
    }

    private void RevealAll()
    {
        StopTyping();

        if (bodyText != null)
        {
            bodyText.ForceMeshUpdate();
            bodyText.maxVisibleCharacters = bodyText.textInfo.characterCount;
        }

        FinishTyping();
    }

    private void FinishTyping()
    {
        _isTyping = false;

        if (continueIndicator != null)
            continueIndicator.SetActive(true);
    }

    private void StopTyping()
    {
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        _isTyping = false;
    }
}
