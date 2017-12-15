using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;

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

        override protected void OnUpdate()
        {
            NativeList<Entity> toDelete = new NativeList<Entity>(Allocator.Temp);
            for (int i = 0; i < particles.Length; ++i)
            {
                var age = particles.age[i];
                age.age += Time.deltaTime;
                if (age.age > age.maxAge)
                    toDelete.Add(particles.entities[i]);
                particles.age[i] = age;
            }
            for (int i = 0; i < toDelete.Length; ++i)
                m_EntityManager.DestroyEntity(toDelete[i]);
            toDelete.Dispose();
        }
    }

    [UpdateBefore(typeof(ParticleRenderSystem))]
    [UpdateInGroup(typeof(ParticleUpdateSystemGroup))]
    public class ParticleMoveSystem : ComponentSystem
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

        override protected void OnUpdate()
        {
            for (int i = 0; i < particles.Length; ++i)
            {
                var position = particles.position[i];
                position.x += particles.velocity[i].velocity.x * Time.deltaTime;
                position.y += particles.velocity[i].velocity.y * Time.deltaTime;
                particles.position[i] = position;
            }
        }
    }
    [UpdateBefore(typeof(ParticleRenderSystem))]
    [UpdateAfter(typeof(ParticleAgeSystem))]
    [UpdateInGroup(typeof(ParticleUpdateSystemGroup))]
    public class ParticleColorTransitionSystem : ComponentSystem
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

        override protected void OnUpdate()
        {
            for (int i = 0; i < particles.Length; ++i)
            {
                var particle = particles.particle[i];
                var color = particles.color[i];
                var age = particles.age[i];
                float colorScale = age.age / age.maxAge;
                particle.color = color.startColor + (color.endColor - color.startColor) * colorScale;
                particles.particle[i] = particle;
            }
        }
    }
    [UpdateBefore(typeof(ParticleRenderSystem))]
    [UpdateAfter(typeof(ParticleAgeSystem))]
    [UpdateInGroup(typeof(ParticleUpdateSystemGroup))]
    public class ParticleSizeTransitionSystem : ComponentSystem
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

        override protected void OnUpdate()
        {
            for (int i = 0; i < particles.Length; ++i)
            {
                var particle = particles.particle[i];
                var size = particles.size[i];
                var age = particles.age[i];
                float sizeScale = age.age / age.maxAge;
                particle.length = size.startLength + (size.endLength - size.startLength) * sizeScale;
                particle.width = size.startWidth + (size.endWidth - size.startWidth) * sizeScale;
                particles.particle[i] = particle;
            }
        }
    }
}
