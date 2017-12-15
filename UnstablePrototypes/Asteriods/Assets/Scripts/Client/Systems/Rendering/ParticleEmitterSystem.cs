using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;

namespace Asteriods.Client
{
    [UpdateBefore(typeof(ParticleUpdateSystemGroup))]
    public class ParticleEmitterSystem : ComponentSystem
    {
        [Inject]
        EntityManager m_EntityManager;

        struct ParticleEmitters
        {
            public int Length;
            [ReadOnly]
            public ComponentDataArray<ParticleEmitterComponentData> emitter;
            [ReadOnly]
            public ComponentDataArray<PositionComponentData> position;
            [ReadOnly]
            public ComponentDataArray<RotationComponentData> rotation;
            //[ReadOnly]
            //public ComponentDataArray<VelocityComponentData> velocity;
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
                    spawner.position = new float2(emitters.position[em].x, emitters.position[em].y);
                    spawner.rotation = emitters.rotation[em].angle;
                    spawner.velocity = new float2(0, 0);//new float2(emitters.velocity[em].dx, emitters.velocity[em].dy);
                    spawners.Add(spawner);
                }
            }
            for (int em = 0; em < spawners.Length; ++em)
            {
                int particles = (int)(Time.deltaTime * spawners[em].emitter.particlesPerSecond + 0.5f);
                float2 spawnOffset = RotationComponentData.rotate(spawners[em].emitter.spawnOffset, spawners[em].rotation);

                bool colorTrans = math.any(spawners[em].emitter.startColor != spawners[em].emitter.endColor);
                bool sizeTrans = spawners[em].emitter.startLength != spawners[em].emitter.endLength || spawners[em].emitter.startWidth != spawners[em].emitter.endWidth;
                EntityArchetype particleArchetype;
                if (colorTrans && sizeTrans)
                    particleArchetype = m_EntityManager.CreateArchetype(typeof(ParticleComponentData),
                        typeof(ParticleAgeComponentData), typeof(ParticleVelocityComponentData),
                        typeof(PositionComponentData), typeof(RotationComponentData),
                        typeof(ParticleColorTransitionComponentData), typeof(ParticleSizeTransitionComponentData));
                else if (colorTrans)
                    particleArchetype = m_EntityManager.CreateArchetype(typeof(ParticleComponentData),
                        typeof(ParticleAgeComponentData), typeof(ParticleVelocityComponentData),
                        typeof(PositionComponentData), typeof(RotationComponentData),
                        typeof(ParticleColorTransitionComponentData));
                else if (sizeTrans)
                    particleArchetype = m_EntityManager.CreateArchetype(typeof(ParticleComponentData),
                        typeof(ParticleAgeComponentData), typeof(ParticleVelocityComponentData),
                        typeof(PositionComponentData), typeof(RotationComponentData),
                        typeof(ParticleSizeTransitionComponentData));
                else
                    particleArchetype = m_EntityManager.CreateArchetype(typeof(ParticleComponentData),
                        typeof(ParticleAgeComponentData), typeof(ParticleVelocityComponentData),
                        typeof(PositionComponentData), typeof(RotationComponentData));
                for (int i = 0; i < particles; ++i)
                {
                    float particleRot = spawners[em].rotation + Random.Range(-spawners[em].emitter.angleSpread, spawners[em].emitter.angleSpread);
                    float particleVelocity = spawners[em].emitter.velocityBase + Random.Range(0, spawners[em].emitter.velocityRandom);
                    float2 particleDir = new float2(0, particleVelocity);
                    particleDir = RotationComponentData.rotate(particleDir, particleRot);
                    particleDir += spawners[em].velocity;
                    var particle = m_EntityManager.CreateEntity(particleArchetype);
                    m_EntityManager.SetComponent(particle, new ParticleComponentData(spawners[em].emitter.startLength, spawners[em].emitter.startWidth, spawners[em].emitter.startColor));
                    m_EntityManager.SetComponent(particle, new ParticleAgeComponentData(spawners[em].emitter.particleLifetime));
                    // TODO: only if velocity is > epsilon
                    m_EntityManager.SetComponent(particle, new ParticleVelocityComponentData(particleDir));
                    m_EntityManager.SetComponent(particle, new PositionComponentData(spawners[em].position.x + spawnOffset.x +
                        Random.Range(-spawners[em].emitter.spawnSpread, spawners[em].emitter.spawnSpread), spawners[em].position.y + spawnOffset.y +
                        Random.Range(-spawners[em].emitter.spawnSpread, spawners[em].emitter.spawnSpread)));
                    m_EntityManager.SetComponent(particle, new RotationComponentData(particleRot));
                    if (colorTrans)
                        m_EntityManager.SetComponent(particle,
                            new ParticleColorTransitionComponentData(spawners[em].emitter.startColor, spawners[em].emitter.endColor));
                    if (sizeTrans)
                        m_EntityManager.SetComponent(particle,
                            new ParticleSizeTransitionComponentData(spawners[em].emitter.startLength,
                            spawners[em].emitter.endLength, spawners[em].emitter.startWidth, spawners[em].emitter.endWidth));
                }
            }
            spawners.Dispose();
        }
    }
}
