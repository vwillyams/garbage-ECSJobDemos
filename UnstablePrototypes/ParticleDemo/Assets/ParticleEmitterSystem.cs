using System;
using System.Net;
using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Random = UnityEngine.Random;

[UpdateBefore(typeof(ParticleUpdateSystemGroup))]
public class ParticleEmitterSystem : JobComponentSystem
{
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

    // EntityCommandBuffer.SetComponent is not comaptible with burst yet
    //[ComputeJobOptimization]
    struct ParticleSpawnJob : IJob
    {
        [ReadOnly]
        public ComponentDataArray<ParticleEmitterComponentData> emitters;
        [ReadOnly]
        public ComponentDataArray<PositionComponentData> positions;
        [ReadOnly]
        public ComponentDataArray<RotationComponentData> rotations;
        [ReadOnly]
        public NativeArray<float> randomData;
        public int randomBase;
        public int curRandom;

        public float deltaTime;
        public EntityArchetype m_ColorSizeParticleArchetype;
        public EntityArchetype m_ColorParticleArchetype;
        public EntityArchetype m_SizeParticleArchetype;
        public EntityArchetype m_ParticleArchetype;

        public EntityCommandBuffer CommandBuffer;

        public int begin;
        public int end;

        float RandomRange(float minVal, float maxVal)
        {
            float rnd = randomData[curRandom % randomData.Length];
            curRandom = (curRandom+1)%randomData.Length;
            return rnd * (maxVal - minVal) + minVal;
        }
        public void Execute()
        {
            for (int i = begin; i < end; ++i)
            {
                if (emitters[i].active == 0)
                    return;

                int particles = (int) (deltaTime * emitters[i].particlesPerSecond + 0.5f);
                if (particles == 0)
                    return;
                float2 spawnOffset = RotationComponentData.rotate(emitters[i].spawnOffset, rotations[i].angle);

                bool colorTrans = math.any(emitters[i].startColor != emitters[i].endColor);
                bool sizeTrans = emitters[i].startLength != emitters[i].endLength ||
                                 emitters[i].startWidth != emitters[i].endWidth;
                EntityArchetype particleArchetype;
                if (colorTrans && sizeTrans)
                    particleArchetype = m_ColorSizeParticleArchetype;
                else if (colorTrans)
                    particleArchetype = m_ColorParticleArchetype;
                else if (sizeTrans)
                    particleArchetype = m_SizeParticleArchetype;
                else
                    particleArchetype = m_ParticleArchetype;

                curRandom = randomBase + i;
                for (int part = 0; part < particles; ++part)
                {
                    CommandBuffer.CreateEntity(particleArchetype);
                    if (colorTrans)
                        CommandBuffer.SetComponent(new ParticleColorTransitionComponentData(emitters[i].startColor,
                            emitters[i].endColor));
                    if (sizeTrans)
                        CommandBuffer.SetComponent(new ParticleSizeTransitionComponentData(emitters[i].startLength,
                            emitters[i].endLength, emitters[i].startWidth,
                            emitters[i].endWidth));
                    //float2 spawnOffset = RotationComponentData.rotate(spawners[em].emitter.spawnOffset, spawners[em].rotation);
                    float particleRot = rotations[i].angle + RandomRange(-emitters[i].angleSpread,
                                            emitters[i].angleSpread);
                    float particleVelocity = emitters[i].velocityBase +
                                             RandomRange(0, emitters[i].velocityRandom);
                    float2 particleDir = new float2(0, particleVelocity);
                    particleDir = RotationComponentData.rotate(particleDir, particleRot);
                    //particleDir += velocities[i].velocity;

                    CommandBuffer.SetComponent(new ParticleComponentData(emitters[i].startLength,
                        emitters[i].startWidth, emitters[i].startColor));
                    CommandBuffer.SetComponent(new ParticleAgeComponentData(emitters[i].particleLifetime));
                    CommandBuffer.SetComponent(new ParticleVelocityComponentData(particleDir));
                    CommandBuffer.SetComponent(new PositionComponentData(
                        positions[i].x + spawnOffset.x + RandomRange(-emitters[i].spawnSpread,
                            emitters[i].spawnSpread),
                        positions[i].y + spawnOffset.y + RandomRange(-emitters[i].spawnSpread,
                            emitters[i].spawnSpread)));
                    CommandBuffer.SetComponent(new RotationComponentData(particleRot));
                }
            }
        }

    }

    [Inject]
    ParticleEmitters emitters;

    [Inject] private EndFrameBarrier m_EndFrameBarrier;

    EntityArchetype m_ColorSizeParticleArchetype;
    EntityArchetype m_ColorParticleArchetype;
    EntityArchetype m_SizeParticleArchetype;
    EntityArchetype m_ParticleArchetype;
    override protected void OnCreateManager(int capacity)
    {
        m_ColorSizeParticleArchetype = EntityManager.CreateArchetype(typeof(ParticleComponentData),
            typeof(ParticleAgeComponentData), typeof(ParticleVelocityComponentData),
            typeof(PositionComponentData), typeof(RotationComponentData),
            typeof(ParticleColorTransitionComponentData), typeof(ParticleSizeTransitionComponentData));
        m_ColorParticleArchetype = EntityManager.CreateArchetype(typeof(ParticleComponentData),
            typeof(ParticleAgeComponentData), typeof(ParticleVelocityComponentData),
            typeof(PositionComponentData), typeof(RotationComponentData),
            typeof(ParticleColorTransitionComponentData));
        m_SizeParticleArchetype = EntityManager.CreateArchetype(typeof(ParticleComponentData),
            typeof(ParticleAgeComponentData), typeof(ParticleVelocityComponentData),
            typeof(PositionComponentData), typeof(RotationComponentData),
            typeof(ParticleSizeTransitionComponentData));
        m_ParticleArchetype = EntityManager.CreateArchetype(typeof(ParticleComponentData),
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
        int numJobs = Math.Min(emitters.Length, 16);
        int itemsPerJob = (emitters.Length + numJobs - 1) / numJobs;
        NativeArray<JobHandle> handles = new NativeArray<JobHandle>(numJobs, Allocator.Temp);
        for (int i = 0; i < numJobs; ++i)
        {
            var spawnJob = new ParticleSpawnJob
            {
                emitters = emitters.emitter,
                positions = emitters.position,
                rotations = emitters.rotation,
                randomData = randomData,
                randomBase = randomDataBase,
                curRandom = 0,
                deltaTime = Time.deltaTime,
                m_ColorSizeParticleArchetype = m_ColorSizeParticleArchetype,
                m_ColorParticleArchetype = m_ColorParticleArchetype,
                m_SizeParticleArchetype = m_SizeParticleArchetype,
                m_ParticleArchetype = m_ParticleArchetype,
                CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer(),
                begin = i*itemsPerJob,
                end = Math.Min((i+1)*itemsPerJob, emitters.Length)
            };
            randomDataBase = (randomDataBase + emitters.Length) % randomData.Length;
            handles[i] = spawnJob.Schedule(inputDep);
        }

        JobHandle jh = JobHandle.CombineDependencies(handles);
        handles.Dispose();
        return jh;
    }
}
