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
    public class ParticleRenderSystem : ComponentSystem
    {
        [Inject]
        LineRenderSystem m_LineRenderSystem;

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

        override protected void OnUpdate()
        {
            NativeList<LineRenderSystem.Line> lines = m_LineRenderSystem.LineList;
            for (int i = 0; i < particles.Length; ++i)
            {
                var particle = particles.particle[i];
                var position = particles.position[i];
                float2 pos = new float2(position.x, position.y);
                float2 dir = new float2(0, particle.length);
                dir = RotationComponentData.rotate(dir, particles.rotation[i].angle);
                lines.Add(new LineRenderSystem.Line(pos, pos - dir, particle.color, particle.width));
            }
        }
    }

    [UpdateBefore(typeof(ParticleRenderSystem))]
    [UpdateInGroup(typeof(ParticleUpdateSystemGroup))]
    public class ParticleAgeSystem : ComponentSystem
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
                if (age.age > age.maxAge)
                    toDelete.Enqueue(entities[i]);
                ages[i] = age;
            }
        }
        override protected void OnUpdate()
        {
            NativeQueue<Entity> toDelete = new NativeQueue<Entity>(particles.Length, Allocator.TempJob);
            var job = new ParticleAgeJob();
            job.toDelete = toDelete;
            job.deltaTime = Time.deltaTime;
            job.ages = particles.age;
            job.entities = particles.entities;
            job.Schedule(particles.Length, 8).Complete();
            Entity ent;
            while (toDelete.TryDequeue(out ent))
            {
                m_EntityManager.DestroyEntity(ent);
            }
            toDelete.Dispose();
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
        struct Particles
        {
            public int Length;
            [ReadOnly]
            public ComponentDataArray<ParticleSizeTransitionComponentData> size;
            [ReadOnly]
            public ComponentDataArray<ParticleAgeComponentData> age;
            public ComponentDataArray<ParticleComponentData> particle;
        }

        [InjectComponentGroup]
        Particles particles;

        struct ParticleSizeJob : IJobParallelFor
        {
            public ComponentDataArray<ParticleComponentData> particles;
            [ReadOnly]
            public ComponentDataArray<ParticleSizeTransitionComponentData> sizes;
            [ReadOnly]
            public ComponentDataArray<ParticleAgeComponentData> ages;
            public void Execute(int i)
            {
                var particle = particles[i];
                var size = sizes[i];
                var age = ages[i];
                float sizeScale = age.age / age.maxAge;
                particle.length = size.startLength + (size.endLength - size.startLength) * sizeScale;
                particle.width = size.startWidth + (size.endWidth - size.startWidth) * sizeScale;
                particles[i] = particle;
            }
        }
        override protected JobHandle OnUpdate(JobHandle inputDep)
        {
            var job = new ParticleSizeJob();
            job.particles = particles.particle;
            job.sizes = particles.size;
            job.ages = particles.age;
            return job.Schedule(particles.Length, 8, inputDep);
        }
    }
}
