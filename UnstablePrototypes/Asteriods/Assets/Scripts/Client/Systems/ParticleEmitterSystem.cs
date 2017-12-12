using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;

public class ParticleEmitterSystem : ComponentSystem
{
    [Inject]
    EntityManager m_EntityManager;

    struct ParticleEmitters
    {
        public int Length;
        [ReadOnly]
        public ComponentDataArray<ParticleEmitterComponentData> emitter;
        //[ReadOnly]
        //public ComponentDataArray<PositionComponentData> position;
        //[ReadOnly]
        //public ComponentDataArray<RotationComponentData> rotation;
        //[ReadOnly]
        //public ComponentDataArray<VelocityComponentData> velocity;
        public ComponentArray<Transform> transform;
    }

    [InjectComponentGroup]
    ParticleEmitters emitters;

    struct ParticleSpawner
    {
        public ParticleEmitterComponentData emitter;
        public float2 position;
        public float rotation;
        public float2 velocity;
    }
    override protected void OnUpdate()
    {
        NativeList<ParticleSpawner> spawners = new NativeList<ParticleSpawner>(emitters.Length, Allocator.Temp);
        for (int em = 0; em < emitters.Length; ++em)
        {
            if (emitters.emitter[em].active != 0)
            {
                var spawner = new ParticleSpawner();
                spawner.emitter = emitters.emitter[em];
                spawner.position = LineRenderSystem.screenPosFromTransform(emitters.transform[em].position);//new float2(emitters.position[em].x, emitters.position[em].y);
                spawner.rotation = emitters.transform[em].eulerAngles.z;//emitters.rotation[em].angle;
                spawner.velocity = new float2(0,0);//new float2(emitters.velocity[em].dx, emitters.velocity[em].dy);
                spawners.Add(spawner);
            }
        }
        for (int em = 0; em < spawners.Length; ++em)
        {
            int particles = (int)(Time.deltaTime * spawners[em].emitter.particlesPerSecond + 0.5f);
            float2 spawnOffset = LineRenderSystem.rotatePos(spawners[em].emitter.spawnOffset, Quaternion.Euler(0,0,spawners[em].rotation));
            for (int i = 0; i < particles; ++i)
            {
                float particleRot = spawners[em].rotation + Random.Range(-spawners[em].emitter.angleSpread, spawners[em].emitter.angleSpread);
                float particleVelocity = spawners[em].emitter.velocityBase + Random.Range(0, spawners[em].emitter.velocityRandom);
                float2 particleDir = new float2(0, particleVelocity);
                particleDir = LineRenderSystem.rotatePos(particleDir, Quaternion.Euler(0,0,particleRot));
                particleDir += spawners[em].velocity;
                var particle = m_EntityManager.CreateEntity();
                m_EntityManager.AddComponent(particle, new ParticleComponentData(spawners[em].emitter.startLength, spawners[em].emitter.startWidth, spawners[em].emitter.startColor));
                m_EntityManager.AddComponent(particle, new ParticleAgeComponentData(spawners[em].emitter.particleLifetime));
                // TODO: only if velocity is > epsilon
                m_EntityManager.AddComponent(particle, new ParticleVelocityComponentData(particleDir));
                m_EntityManager.AddComponent(particle, new PositionComponentData(spawners[em].position.x + spawnOffset.x +
                    Random.Range(-spawners[em].emitter.spawnSpread,spawners[em].emitter.spawnSpread), spawners[em].position.y + spawnOffset.y +
                    Random.Range(-spawners[em].emitter.spawnSpread,spawners[em].emitter.spawnSpread)));
                m_EntityManager.AddComponent(particle, new RotationComponentData(particleRot));
                if (math.any(spawners[em].emitter.startColor != spawners[em].emitter.endColor))
                    m_EntityManager.AddComponent(particle,
                        new ParticleColorTransitionComponentData(spawners[em].emitter.startColor, spawners[em].emitter.endColor));
                if (spawners[em].emitter.startLength != spawners[em].emitter.endLength || spawners[em].emitter.startWidth != spawners[em].emitter.endWidth)
                    m_EntityManager.AddComponent(particle,
                        new ParticleSizeTransitionComponentData(spawners[em].emitter.startLength,
                        spawners[em].emitter.endLength, spawners[em].emitter.startWidth, spawners[em].emitter.endWidth));
            }
        }
        spawners.Dispose();
    }
}
