using UnityEngine;

// Drives the global wind values used by the Auffi/Wind Lit shader. Every tree and bush
// shares the same direction, so the whole level reacts to one consistent wind.
[DefaultExecutionOrder(-50)]
public class WindManager : MonoBehaviour
{
    [Header("Direction")]
    [Tooltip("Horizontal world-space direction the wind blows towards.")]
    [SerializeField] private Vector3 windDirection = new Vector3(1f, 0f, 0.35f);

    [Header("Strength")]
    [Tooltip("Overall wind strength multiplier.")]
    [SerializeField, Range(0f, 2f)] private float strength = 1f;
    [Tooltip("Horizontal displacement in metres for the fully bent upper part of a plant.")]
    [SerializeField, Range(0f, 2f)] private float bendAmount = 0.45f;

    [Header("Motion")]
    [Tooltip("How fast the plants sway back and forth.")]
    [SerializeField, Min(0f)] private float swayFrequency = 1.1f;
    [Tooltip("How much the wind strength rises and falls over time.")]
    [SerializeField, Range(0f, 1f)] private float gustStrength = 0.5f;
    [Tooltip("How fast the gusts roll through the level.")]
    [SerializeField, Min(0f)] private float gustFrequency = 0.15f;

    private static readonly int DirectionId = Shader.PropertyToID("_AuffiWindDirection");
    private static readonly int StrengthId = Shader.PropertyToID("_AuffiWindStrength");
    private static readonly int BendId = Shader.PropertyToID("_AuffiWindBend");
    private static readonly int FrequencyId = Shader.PropertyToID("_AuffiWindFrequency");
    private static readonly int GustStrengthId = Shader.PropertyToID("_AuffiWindGustStrength");
    private static readonly int GustFrequencyId = Shader.PropertyToID("_AuffiWindGustFrequency");

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void Apply()
    {
        Vector3 direction = windDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;
        direction.Normalize();

        Shader.SetGlobalVector(DirectionId, new Vector4(direction.x, 0f, direction.z, 0f));
        Shader.SetGlobalFloat(StrengthId, strength);
        Shader.SetGlobalFloat(BendId, bendAmount);
        Shader.SetGlobalFloat(FrequencyId, swayFrequency);
        Shader.SetGlobalFloat(GustStrengthId, gustStrength);
        Shader.SetGlobalFloat(GustFrequencyId, gustFrequency);
    }
}
