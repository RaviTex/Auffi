using UnityEngine;

public class AnimalTravel : MonoBehaviour
{
    [Tooltip("Destination used by TravelToDestination(). Set this per animal in the editor.")]
    [SerializeField] private Transform destination;
    [SerializeField] private float moveSpeed = 2f;
    [Tooltip("How close to the destination the animal stops (right next to it).")]
    [SerializeField] private float stopDistance = 1.5f;
    [SerializeField] private float rotationLerpSpeed = 8f;

    private Transform _target;

    public bool IsTraveling => _target != null;

    // Parameterless entry point so it can be wired to any UnityEvent in the inspector.
    public void TravelToDestination()
    {
        TravelTo(destination);
    }

    public void TravelTo(Transform target)
    {
        _target = target;
    }

    public void Stop()
    {
        _target = null;
    }

    private void Update()
    {
        if (_target == null)
            return;

        Vector3 toTarget = _target.position - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= stopDistance)
        {
            _target = null;
            return;
        }

        Vector3 direction = toTarget / distance;
        float travel = Mathf.Min(moveSpeed * Time.deltaTime, distance - stopDistance);
        transform.position += direction * travel;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
    }
}
