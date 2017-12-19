using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;
using Unity.Jobs;

namespace Asteriods.Client
{
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
        [InjectComponentGroup]
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

        [InjectComponentGroup]
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
        [Inject]
        EntityManager m_EntityManager;

        struct Particles
        {
            public int Length;
            public ComponentDataArray<ParticleAgeComponentData> age;
            public EntityArray entities;
        }

        [InjectComponentGroup]
        Particles particles;


        [ComputeJobOptimization]
        struct ParticleAgeJob : IJobParallelFor
        {
            public float deltaTime;
            public NativeQueue<Entity>.Concurrent toDelete;

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
                    toDelete.Enqueue(entities[i]);
                }
                ages[i] = age;
            }
        }
        NativeQueue<Entity> toDelete;

        override protected void OnCreateManager(int capacity)
        {
            toDelete = new NativeQueue<Entity>(10*1024, Allocator.Persistent);
        }
        override protected void OnDestroyManager()
        {
            toDelete.Dispose();
        }
        override protected JobHandle OnUpdate(JobHandle inputDep)
        {
            Entity ent;
            while (toDelete.TryDequeue(out ent))
            {
                m_EntityManager.DestroyEntity(ent);
            }
            UpdateInjectedComponentGroups();
            toDelete.Capacity = System.Math.Max(toDelete.Capacity, particles.Length*2);

            var job = new ParticleAgeJob();
            job.toDelete = toDelete;
            job.deltaTime = Time.deltaTime;
            job.ages = particles.age;
            job.entities = particles.entities;
            return job.Schedule(particles.Length, 8, inputDep);
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

        [InjectComponentGroup]
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

        [InjectComponentGroup]
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

        [InjectComponentGroup]
        Particles particles;

        override protected JobHandle OnUpdate(JobHandle inputDep)
        {
            return particles.Schedule(particles.Length, 8, inputDep);
        }
    }
}
