using UnityEngine;

namespace FlyingChick
{
    // Reference: spawnDust() while diving (~50% chance per 60fps frame),
    // spawnStar() while Fever is active (~60% chance per 60fps frame). Two
    // small always-on ParticleSystems -- not the shared one-shot
    // FX/PickupBurst -- parented to the bird so the emission POINT tracks
    // it, but simulationSpace=World so already-emitted particles stay
    // behind as a trail instead of following the bird around.
    public class BirdTrailParticles : MonoBehaviour
    {
        [SerializeField] private float dustChancePer60fpsFrame = 0.5f;
        [SerializeField] private float starChancePer60fpsFrame = 0.6f;

        private BirdController bird;
        private FeverSystem fever;
        private ParticleSystem dustPs;
        private ParticleSystem starPs;

        public void Configure(BirdController birdRef, FeverSystem feverRef)
        {
            bird = birdRef;
            fever = feverRef;
        }

        private void Awake()
        {
            var dustTex = ProceduralSprite.CreateCircle(10, new Color(0.88f, 0.84f, 0.76f, 0.85f)).texture;
            dustPs = CreateSystem("DustTrail", dustTex, lifetime: 0.4f, speed: 1.5f, size: 0.4f, gravity: 0.3f, sortOrder: 8);

            var starTex = ProceduralSprite.CreateCircle(10, new Color(1f, 0.85f, 0.3f)).texture;
            starPs = CreateSystem("StarTrail", starTex, lifetime: 0.7f, speed: 1f, size: 0.35f, gravity: 0.1f, sortOrder: 11);
        }

        private ParticleSystem CreateSystem(string name, Texture2D tex, float lifetime, float speed, float size, float gravity, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;

            var emission = ps.emission;
            emission.enabled = false; // manual Emit only, driven by Update() below

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
            renderer.material = mat;
            renderer.sortingOrder = sortOrder;

            return ps;
        }

        private void Update()
        {
            if (bird == null) return;

            // Reference probabilities were per-frame at an assumed 60fps;
            // scale by dt so the expected rate doesn't change with framerate.
            float frameScale = Time.deltaTime * 60f;

            if (bird.IsDiving && Random.value < dustChancePer60fpsFrame * frameScale)
                dustPs.Emit(1);

            if (fever != null && fever.IsActive && Random.value < starChancePer60fpsFrame * frameScale)
                starPs.Emit(1);
        }
    }
}
