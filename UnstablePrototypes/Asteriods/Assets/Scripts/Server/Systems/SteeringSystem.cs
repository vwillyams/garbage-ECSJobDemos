using UnityEngine;
using Unity.ECS;

using Unity.Collections;
using Unity.Mathematics;

namespace Asteriods.Server
{
    [UpdateAfter(typeof(Asteriods.Client.InputSystem))]
    public class SteeringSystem : ComponentSystem
    {
        public NativeQueue<PlayerInputComponentData> playerInputQueue;

        static float displacement = 2.0f;
        struct Spaceships
        {
            public int Length;
            public ComponentDataArray<VelocityComponentData> steering;
            public ComponentDataArray<PositionComponentData> positions;
            public ComponentDataArray<PlayerInputComponentData> inputs;
            public ComponentDataArray<RotationComponentData> rotations;
            ComponentDataArray<PlayerTagComponentData> tags;
        }

        [Inject]
        Spaceships spaceships;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            playerInputQueue = new NativeQueue<PlayerInputComponentData>(Allocator.Persistent);
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

            for (int i = 0, s = spaceships.Length; i < s; ++i)
            {
                var rot = spaceships.rotations[i];
                float angle = rot.angle;//;spaceships.steering[0].angle;
                float dx = spaceships.steering[i].dx;
                float dy = spaceships.steering[i].dy;

                PlayerInputComponentData input = spaceships.inputs[i];

                if (input.left == 1)
                {
                    angle -= displacement;
                }
                if (input.right == 1)
                {
                    angle += displacement;
                }
                if (input.thrust == 1)
                {
                    dx -= math.sin(math.radians(angle)) * ServerSettings.Instance().playerForce * dt;
                    dy += math.cos(math.radians(angle)) * ServerSettings.Instance().playerForce * dt;
                }

                var pos = spaceships.positions[i];

                spaceships.positions[i] = new PositionComponentData(pos.x + dx, pos.y + dy);
                spaceships.rotations[i] = new RotationComponentData(angle);
                spaceships.steering[i] = new VelocityComponentData(dx, dy);
            }


        }
    }
}
