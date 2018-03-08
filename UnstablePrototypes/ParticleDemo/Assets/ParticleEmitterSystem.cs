using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;

[UpdateBefore(typeof(ParticleUpdateSystemGroup))]
public class ParticleEmitterSystem : JobComponentSystem
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
        public SubtractiveComponent<ParticleComponentData> ignoreParticle;
    }

    struct ParticleIntialize
    {
        public int Length;
        [ReadOnly]
        public ComponentDataArray<ParticleEmitterComponentData> emitter;

        public ComponentDataArray<ParticleComponentData> particle;
        public ComponentDataArray<PositionComponentData> position;
        public ComponentDataArray<RotationComponentData> rotation;
        public ComponentDataArray<ParticleVelocityComponentData> velocity;
        public ComponentDataArray<ParticleAgeComponentData> age;
    }

    [ComputeJobOptimization]
    struct ParticleIntializeJob : IJobParallelFor
    {
        public ParticleIntialize components;
        [ReadOnly]
        public NativeArray<float> randomData;
        public int randomBase;
        public int curRandom;

        float RandomRange(float minVal, float maxVal)
        {
            float rnd = randomData[curRandom % randomData.Length];
            curRandom = (curRandom+1)%randomData.Length;
            return rnd * (maxVal - minVal) + minVal;
        }
        public void Execute(int i)
        {
            // Already initialize
            // FIXME: could go away if the emitter component was removed
            if (components.age[i].maxAge > 0)
                return;
            curRandom = randomBase + i;
            //float2 spawnOffset = RotationComponentData.rotate(spawners[em].emitter.spawnOffset, spawners[em].rotation);
            float particleRot = components.rotation[i].angle + RandomRange(-components.emitter[i].angleSpread, components.emitter[i].angleSpread);
            float particleVelocity = components.emitter[i].velocityBase + RandomRange(0, components.emitter[i].velocityRandom);
            float2 particleDir = new float2(0, particleVelocity);
            particleDir = RotationComponentData.rotate(particleDir, particleRot);
            particleDir += components.velocity[i].velocity;

            components.particle[i] = new ParticleComponentData(components.emitter[i].startLength, components.emitter[i].startWidth, components.emitter[i].startColor);
            components.age[i] = new ParticleAgeComponentData(components.emitter[i].particleLifetime);
            components.velocity[i] = new ParticleVelocityComponentData(particleDir);
            components.position[i] = new PositionComponentData(components.position[i].x + RandomRange(-components.emitter[i].spawnSpread, components.emitter[i].spawnSpread),
                components.position[i].y + RandomRange(-components.emitter[i].spawnSpread, components.emitter[i].spawnSpread));
            components.rotation[i] = new RotationComponentData(particleRot);
        }

    }

    [Inject]
    ParticleEmitters emitters;

    [Inject]
    ParticleIntialize initializers;

    struct ParticleSpawner
    {
        public ParticleEmitterComponentData emitter;
        public float2 position;
        public float rotation;
        public float2 velocity;
    }

    EntityArchetype m_ColorSizeParticleArchetype;
    EntityArchetype m_ColorParticleArchetype;
    EntityArchetype m_SizeParticleArchetype;
    EntityArchetype m_ParticleArchetype;
    override protected void OnCreateManager(int capacity)
    {
        m_ColorSizeParticleArchetype = m_EntityManager.CreateArchetype(typeof(ParticleEmitterComponentData), typeof(ParticleComponentData),
            typeof(ParticleAgeComponentData), typeof(ParticleVelocityComponentData),
            typeof(PositionComponentData), typeof(RotationComponentData),
            typeof(ParticleColorTransitionComponentData), typeof(ParticleSizeTransitionComponentData));
        m_ColorParticleArchetype = m_EntityManager.CreateArchetype(typeof(ParticleEmitterComponentData), typeof(ParticleComponentData),
            typeof(ParticleAgeComponentData), typeof(ParticleVelocityComponentData),
            typeof(PositionComponentData), typeof(RotationComponentData),
            typeof(ParticleColorTransitionComponentData));
        m_SizeParticleArchetype = m_EntityManager.CreateArchetype(typeof(ParticleEmitterComponentData), typeof(ParticleComponentData),
            typeof(ParticleAgeComponentData), typeof(ParticleVelocityComponentData),
            typeof(PositionComponentData), typeof(RotationComponentData),
            typeof(ParticleSizeTransitionComponentData));
        m_ParticleArchetype = m_EntityManager.CreateArchetype(typeof(ParticleEmitterComponentData), typeof(ParticleComponentData),
            typeof(ParticleAgeComponentData), typeof(ParticleVelocityComponentData),
            typeof(PositionComponentData), typeof(RotationComponentData));

        randomData = new NativeArray<float>(10*1024, Allocator.Persistent);
        for (int i = 0; i < randomData.Length; ++i)
            randomData[i] = Random.Range(0.0f, 1.0f);
        randomDataBase = 0;
    }
    NativeArray<float> randomData;
    int randomDataBase;

    override protected void OnDestroyManager()
    {
        randomData.Dispose();
    }
    override protected JobHandle OnUpdate(JobHandle inputDep)
    {
        inputDep.Complete();
        // FIXME: should really batch remove the ParticleEmitterComponent from everything in the initializers list here since they have already been initialized
        NativeList<ParticleSpawner> spawners = new NativeList<ParticleSpawner>(emitters.Length, Allocator.Temp);
        NativeList<Entity> particleEntities = new NativeList<Entity>(1024, Allocator.Temp);
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
            if (particles == 0)
                continue;
            float2 spawnOffset = RotationComponentData.rotate(spawners[em].emitter.spawnOffset, spawners[em].rotation);

            bool colorTrans = math.any(spawners[em].emitter.startColor != spawners[em].emitter.endColor);
            bool sizeTrans = spawners[em].emitter.startLength != spawners[em].emitter.endLength || spawners[em].emitter.startWidth != spawners[em].emitter.endWidth;
            EntityArchetype particleArchetype;
            if (colorTrans && sizeTrans)
                particleArchetype = m_ColorSizeParticleArchetype;
            else if (colorTrans)
                particleArchetype = m_ColorParticleArchetype;
            else if (sizeTrans)
                particleArchetype = m_SizeParticleArchetype;
            else
                particleArchetype = m_ParticleArchetype;
            // Create the first particle, then instantiate the rest based on its value
            var particle = m_EntityManager.CreateEntity(particleArchetype);
            m_EntityManager.SetComponentData(particle, spawners[em].emitter);
            // Set initial data
            m_EntityManager.SetComponentData(particle, new ParticleVelocityComponentData(spawners[em].velocity));
            m_EntityManager.SetComponentData(particle, new PositionComponentData(spawners[em].position.x + spawnOffset.x, spawners[em].position.y + spawnOffset.y));
            m_EntityManager.SetComponentData(particle, new RotationComponentData(spawners[em].rotation));
            if (colorTrans)
                m_EntityManager.SetComponentData(particle,
                    new ParticleColorTransitionComponentData(spawners[em].emitter.startColor, spawners[em].emitter.endColor));
            if (sizeTrans)
                m_EntityManager.SetComponentData(particle,
                    new ParticleSizeTransitionComponentData(spawners[em].emitter.startLength,
                    spawners[em].emitter.endLength, spawners[em].emitter.startWidth, spawners[em].emitter.endWidth));
            if (particles > 1)
            {
                particleEntities.ResizeUninitialized(particles-1);
                NativeArray<Entity> temp = particleEntities;
                m_EntityManager.Instantiate(particle, temp);
            }
        }
        spawners.Dispose();
        particleEntities.Dispose();
        UpdateInjectedComponentGroups();
        if (initializers.Length > 0)
        {
            var job = new ParticleIntializeJob();
            job.components = initializers;
            job.randomData = randomData;
            job.randomBase = randomDataBase;
            randomDataBase = (randomDataBase + initializers.Length) % randomData.Length;
            return job.Schedule(initializers.Length, 8);
        }
        return new JobHandle();
    }
}
