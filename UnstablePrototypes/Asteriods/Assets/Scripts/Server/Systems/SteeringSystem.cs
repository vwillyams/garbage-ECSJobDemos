using UnityEngine;
using UnityEngine.ECS;

using Unity.Mathematics;

namespace Asteriods.Server
{
    [UpdateAfter(typeof(Asteriods.Client.InputSystem))]
    public class SteeringSystem : ComponentSystem
    {
        static float force = 0.1f;
        static float displacement = 2.0f;
        struct Spaceships
        {
            public int Length;
            public ComponentDataArray<SteeringComponentData> steering;
            public ComponentDataArray<PlayerInputComponentData> input;
            public ComponentArray<Transform> transform;
        }

        [InjectComponentGroup]
        Spaceships spaceships;

        override protected void OnUpdate()
        {
            if (spaceships.Length <= 0)
                return;
            float dt = Time.deltaTime;

            float angle = spaceships.steering[0].angle;
            float dx = spaceships.steering[0].dx;
            float dy = spaceships.steering[0].dy;

            PlayerInputComponentData input = spaceships.input[0];

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

            spaceships.steering[0] = new SteeringComponentData(angle, dx, dy);

            for (int i = 0; i < spaceships.transform.Length; i++)
            {
                var pos = spaceships.transform[i].position;

                spaceships.transform[i].position = new Vector3(pos.x + spaceships.steering[i].dx, pos.y + spaceships.steering[i].dy, 0);
                spaceships.transform[i].rotation = Quaternion.Euler(0f, 0f, spaceships.steering[i].angle);
            }
        }
    }
}