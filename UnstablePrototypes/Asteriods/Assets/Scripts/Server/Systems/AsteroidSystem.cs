using UnityEngine;
using UnityEngine.ECS;

using Unity.Mathematics;

namespace Asteriods.Server
{
    public class AsteroidSystem : ComponentSystem
    {
        struct Asteroids
        {
            public int Length;
            public ComponentDataArray<VelocityComponentData> velocities;
            public ComponentDataArray<PositionComponentData> positions;
            public ComponentDataArray<RotationComponentData> rotations;
            ComponentDataArray<AsteroidTagComponentData> tags;
        }

        [Inject]
        Asteroids asteroids;

        override protected void OnUpdate()
        {
            if (asteroids.Length <= 0)
                return;

            float dt = Time.deltaTime;

            for (int i = 0; i < asteroids.Length; i++)
            {
                var pos = asteroids.positions[i];
                var rot = asteroids.rotations[i];

                asteroids.positions[i] = new PositionComponentData(pos.x + asteroids.velocities[i].dx, pos.y + asteroids.velocities[i].dy);
                asteroids.rotations[i] = new RotationComponentData(rot.angle + 2);
            }
        }
    }
}
