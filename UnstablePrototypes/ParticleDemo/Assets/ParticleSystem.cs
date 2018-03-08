using System;
using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;

public class ParticleUpdateSystemGroup
{}

[UpdateBefore(typeof(LineRenderSystem))]
[UpdateInGroup(typeof(ParticleUpdateSystemGroup))]
public class ParticleRenderSystem : JobComponentSystem
{
    [Inject]
    LineRenderSystem m_LineRenderSystem;

    struct LineList
    {
        public ComponentDataArray<LineRendererComponentData> line;
    }
    [Inject]
    LineList m_LineListComponent;
    struct Particles
    {
        public int Length;
        [ReadOnly]
        public ComponentDataArray<ParticleComponentData> particle;
        [ReadOnly]
        public ComponentDataArray<PositionComponentData> position;
        [ReadOnly]
        public ComponentDataArray<RotationComponentData> rotation;
    }

    [Inject]
    Particles particles;

    [ComputeJobOptimization]
    struct ParticleRenderJob : IJobParallelFor
    {
        public NativeSlice<LineRenderSystem.Line> lines;
        [ReadOnly]
        public ComponentDataArray<ParticleComponentData> particles;
        [ReadOnly]
        public ComponentDataArray<PositionComponentData> positions;
        [ReadOnly]
        public ComponentDataArray<RotationComponentData> rotations;
        public void Execute(int i)
        {
            var particle = particles[i];
            var position = positions[i];
            float2 pos = new float2(position.x, position.y);
            float2 dir = new float2(0, particle.length);
            dir = RotationComponentData.rotate(dir, rotations[i].angle);
            lines[i] = new LineRenderSystem.Line(pos, pos - dir, particle.color, particle.width);
        }
    }
    override protected JobHandle OnUpdate(JobHandle inputDep)
    {
        if (m_LineListComponent.line.Length != 1)
            return inputDep;
        NativeList<LineRenderSystem.Line> lines = m_LineRenderSystem.LineList;
        var start = lines.Length;
        lines.ResizeUninitialized(start + particles.Length);

        NativeSlice<LineRenderSystem.Line> lineSubset = new NativeSlice<LineRenderSystem.Line>(lines, start, particles.Length);

        var job = new ParticleRenderJob();
        job.lines = lineSubset;
        job.particles = particles.particle;
        job.positions = particles.position;
        job.rotations = particles.rotation;
        return job.Schedule(particles.Length, 8, inputDep);
    }
}

[UpdateBefore(typeof(ParticleRenderSystem))]
[UpdateInGroup(typeof(ParticleUpdateSystemGroup))]
public class ParticleAgeSystem : JobComponentSystem
{
    struct Particles
    {
        public int Length;
        public ComponentDataArray<ParticleAgeComponentData> age;
        public EntityArray entities;
    }

    [Inject]
    Particles particles;

    [ComputeJobOptimization]
    struct ParticleAgeJob : IJobParallelFor
    {
        public float deltaTime;

        [ReadOnly]
        public EntityArray entities;
        public ComponentDataArray<ParticleAgeComponentData> ages;
        public void Execute(int i)
        {
            var age = ages[i];
            age.age += deltaTime;
            if (age.age >= age.maxAge)
            {
                age.age = age.maxAge;
            }
            ages[i] = age;
        }
    }

    override protected JobHandle OnUpdate(JobHandle inputDep)
    {
        var job = new ParticleAgeJob();
        job.deltaTime = Time.deltaTime;
        job.ages = particles.age;
        job.entities = particles.entities;
        return job.Schedule(particles.Length, 8, inputDep);
    }
}
[UpdateAfter(typeof(ParticleAgeSystem))]
[UpdateInGroup(typeof(ParticleUpdateSystemGroup))]
public class ParticleKillSystem : JobComponentSystem
{
    struct Particles
    {
        public int Length;
        [ReadOnly]
        public ComponentDataArray<ParticleAgeComponentData> age;
        public EntityArray entities;
    }

    [Inject]
    Particles particles;
    [Inject] private EndFrameBarrier m_EndFrameBarrier;

    // CommandBuffer is not compatible with burst
    //[ComputeJobOptimization]
    struct ParticleKillJob : IJob
    {
        public EntityCommandBuffer CommandBuffer;

        [ReadOnly]
        public EntityArray entities;
        [ReadOnly]
        public ComponentDataArray<ParticleAgeComponentData> ages;

        public int begin;
        public int end;
        public void Execute()
        {
            for (int i = begin; i < end; ++i)
            {
                var age = ages[i];
                if (age.age >= age.maxAge)
                {
                    CommandBuffer.DestroyEntity(entities[i]);
                }

            }
        }
    }
    override protected JobHandle OnUpdate(JobHandle inputDep)
    {
        int numJobs = Math.Min(particles.Length, 16);
        int itemsPerJob = (particles.Length + numJobs - 1) / numJobs;
        NativeArray<JobHandle> handles = new NativeArray<JobHandle>(numJobs, Allocator.Temp);
        for (int i = 0; i < numJobs; ++i)
        {
            var killJob = new ParticleKillJob
            {
                CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer(),
                entities = particles.entities,
                ages = particles.age,
                begin = i*itemsPerJob,
                end = Math.Min((i+1)*itemsPerJob, particles.Length)
            };
            handles[i] = killJob.Schedule(inputDep);
        }

        JobHandle jh = JobHandle.CombineDependencies(handles);
        handles.Dispose();
        return jh;
    }
}

[UpdateBefore(typeof(ParticleRenderSystem))]
[UpdateInGroup(typeof(ParticleUpdateSystemGroup))]
public class ParticleMoveSystem : JobComponentSystem
{
    struct Particles
    {
        public int Length;
        [ReadOnly]
        public ComponentDataArray<ParticleVelocityComponentData> velocity;
        public ComponentDataArray<PositionComponentData> position;
    }

    [Inject]
    Particles particles;

    [ComputeJobOptimization]
    struct ParticleMoveJob : IJobParallelFor
    {
        public float deltaTime;

        public ComponentDataArray<PositionComponentData> position;
        [ReadOnly]
        public ComponentDataArray<ParticleVelocityComponentData> velocity;
        public void Execute(int i)
        {
            var pos = position[i];
            pos.x += velocity[i].velocity.x * deltaTime;
            pos.y += velocity[i].velocity.y * deltaTime;
            position[i] = pos;
        }
    }
    override protected JobHandle OnUpdate(JobHandle inputDep)
    {
        var job = new ParticleMoveJob();
        job.deltaTime = Time.deltaTime;
        job.position = particles.position;
        job.velocity = particles.velocity;
        return job.Schedule(particles.Length, 8, inputDep);
    }
}
[UpdateBefore(typeof(ParticleRenderSystem))]
[UpdateAfter(typeof(ParticleAgeSystem))]
[UpdateInGroup(typeof(ParticleUpdateSystemGroup))]
public class ParticleColorTransitionSystem : JobComponentSystem
{
    struct Particles
    {
        public int Length;
        [ReadOnly]
        public ComponentDataArray<ParticleColorTransitionComponentData> color;
        [ReadOnly]
        public ComponentDataArray<ParticleAgeComponentData> age;
        public ComponentDataArray<ParticleComponentData> particle;
    }

    [Inject]
    Particles particles;

    [ComputeJobOptimization]
    struct ParticleColorJob : IJobParallelFor
    {
        public ComponentDataArray<ParticleComponentData> particles;
        [ReadOnly]
        public ComponentDataArray<ParticleColorTransitionComponentData> colors;
        [ReadOnly]
        public ComponentDataArray<ParticleAgeComponentData> ages;
        public void Execute(int i)
        {
            var particle = particles[i];
            var color = colors[i];
            var age = ages[i];
            float colorScale = age.age / age.maxAge;
            particle.color = color.startColor + (color.endColor - color.startColor) * colorScale;
            particles[i] = particle;
        }
    }
    override protected JobHandle OnUpdate(JobHandle inputDep)
    {
        var job = new ParticleColorJob();
        job.particles = particles.particle;
        job.colors = particles.color;
        job.ages = particles.age;
        return job.Schedule(particles.Length, 8, inputDep);
    }
}
[UpdateBefore(typeof(ParticleRenderSystem))]
[UpdateAfter(typeof(ParticleAgeSystem))]
[UpdateInGroup(typeof(ParticleUpdateSystemGroup))]
public class ParticleSizeTransitionSystem : JobComponentSystem
{
    [ComputeJobOptimization]
    struct Particles : IJobParallelFor
    {
        public int Length;
        [ReadOnly]
        public ComponentDataArray<ParticleSizeTransitionComponentData> size;
        [ReadOnly]
        public ComponentDataArray<ParticleAgeComponentData> age;
        public ComponentDataArray<ParticleComponentData> particle;
        public void Execute(int i)
        {
            var curpart = particle[i];
            float sizeScale = age[i].age / age[i].maxAge;
            curpart.length = size[i].startLength + (size[i].endLength - size[i].startLength) * sizeScale;
            curpart.width = size[i].startWidth + (size[i].endWidth - size[i].startWidth) * sizeScale;
            particle[i] = curpart;
        }
    }

    [Inject]
    Particles particles;

    override protected JobHandle OnUpdate(JobHandle inputDep)
    {
        return particles.Schedule(particles.Length, 8, inputDep);
    }
}
