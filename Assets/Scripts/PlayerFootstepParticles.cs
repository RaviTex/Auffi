using UnityEngine;

public class PlayerFootstepParticles : MonoBehaviour
{
    [SerializeField] private ParticleSystem footstepParticles;
    [SerializeField, Min(0)] private int particlesPerStep = 2;

    public void OnFootstep()
    {
        ParticleSystem particles = footstepParticles != null ? footstepParticles : FindParticles();

        if (particles == null)
            return;

        if (!particles.isPlaying)
            particles.Play();

        if (particlesPerStep > 0)
            particles.Emit(particlesPerStep);
    }

    private ParticleSystem FindParticles()
    {
        footstepParticles = transform.root.GetComponentInChildren<ParticleSystem>(true);
        return footstepParticles;
    }
}
