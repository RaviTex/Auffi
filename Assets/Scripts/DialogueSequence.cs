using UnityEngine;

[CreateAssetMenu(fileName = "DialogueSequence", menuName = "Auffi/Dialogue Sequence")]
public class DialogueSequence : ScriptableObject
{
    [SerializeField] private string speakerName = "Ranger";
    [SerializeField] private Texture2D portrait;
    [SerializeField, TextArea(2, 6)] private string[] lines = new string[0];

    public string SpeakerName => speakerName;
    public Texture2D Portrait => portrait;
    public string[] Lines => lines;
}
