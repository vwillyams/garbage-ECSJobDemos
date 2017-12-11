using UnityEngine;
using UnityEngine.ECS;

using Unity.Collections;
using Unity.Mathematics;

namespace Asteriods.Server
{
    [UpdateAfter(typeof(Asteriods.Client.InputSystem))]
    public class SteeringSystem : ComponentSystem
    {
        public NativeQueue<PlayerInputComponentData> playerInputQueue;

        static float force = 0.1f;
        static float displacement = 2.0f;
        struct Spaceships
        {
            public int Length;
            public ComponentDataArray<VelocityComponentData> steering;
            public ComponentDataArray<PositionComponentData> positions;
            public ComponentDataArray<RotationComponentData> rotations;
            ComponentDataArray<PlayerTagComponentData> tags;
        }

        [InjectComponentGroup]
        Spaceships spaceships;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            playerInputQueue = new NativeQueue<PlayerInputComponentData>(128, Allocator.Persistent);
            Debug.Assert(playerInputQueue.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (playerInputQueue.IsCreated)
                playerInputQueue.Dispose();
        }

        override protected void OnUpdate()
        {
            if (spaceships.Length <= 0)
                return;

            float dt = Time.deltaTime;

            var rot = spaceships.rotations[0];
            float angle = rot.angle;//;spaceships.steering[0].angle;
            float dx = spaceships.steering[0].dx;
            float dy = spaceships.steering[0].dy;

            if (playerInputQueue.Count > 0)
            {
                PlayerInputComponentData input = playerInputQueue.Dequeue();

                if (input.left == 1)
                {
                    angle += displacement;
                }
                if (input.right == 1)
                {
                    angle -= displacement;
                }
                if (input.thrust == 1)
                {
                    dy += math.sin(math.radians(angle + 90)) * force * dt;
                    dx += math.cos(math.radians(angle + 90)) * force * dt;
                }
            }

            var pos = spaceships.positions[0];

            spaceships.positions[0] = new PositionComponentData(pos.x + dx, pos.y + dy);
            spaceships.rotations[0] = new RotationComponentData(angle);
            spaceships.steering[0] = new VelocityComponentData(dx, dy);
        }
    }
}