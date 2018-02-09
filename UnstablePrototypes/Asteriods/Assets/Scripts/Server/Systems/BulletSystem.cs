using UnityEngine;
using UnityEngine.ECS;

using Unity.Mathematics;

namespace Asteriods.Server
{
    public class BulletSystem : ComponentSystem
    {
        struct Bullet
        {
            public int Length;
            public ComponentDataArray<VelocityComponentData> velocities;
            public ComponentDataArray<PositionComponentData> positions;
            public ComponentDataArray<RotationComponentData> rotations;
            ComponentDataArray<BulletTagComponentData> tags;
        }


        [Inject]
        Bullet bullets;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
        }

        override protected void OnUpdate()
        {
            if (bullets.Length > 0)
            {
                for (int i = 0; i < bullets.Length; i++)
                {
                    var pos = bullets.positions[i];
                    bullets.positions[i] = new PositionComponentData(pos.x + bullets.velocities[i].dx, pos.y + bullets.velocities[i].dy);
                }
            }
        }
    }
}