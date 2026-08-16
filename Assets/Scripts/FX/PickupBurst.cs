using UnityEngine;

namespace FlyingChick
{
    // One shared, reusable Unity ParticleSystem for all pickup bursts
    // (coin/speed/cloud). Emission is manual (Burst()) rather than looping,
    // so there's no per-pickup Instantiate/Destroy -- the particle system
    // itself is Unity's own pooled emission buffer.
    public class PickupBurst : MonoBehaviour
    {
        [SerializeField] private float startLifetime = 0.5f;
        [SerializeField] private float startSpeed = 3f;
        [SerializeField] private float startSize = 0.5f;

        private ParticleSystem ps;

        private void Awake()
        {
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = false;
            main.startLifetime = startLifetime;
            main.startSpeed = startSpeed;
            main.startSize = startSize;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.6f;

            var emission = ps.emission;
            emission.enabled = false; // Burst() calls Emit() manually

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Sprites/Default");
            renderer.material = new Material(shader);
        }

        public void Burst(Vector3 worldPos, Color color, int count)
        {
            transform.position = worldPos;
            var main = ps.main;
            main.startColor = color;
            ps.Emit(count);
        }
    }
}
