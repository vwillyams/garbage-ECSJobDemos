using UnityEngine;
using UnityEngine.ECS;

using Unity.Mathematics;

namespace Asteriods.Server
{
    public class AsteroidSystem : ComponentSystem
    {
        static float force = 0.01f;
        struct Asteroids
        {
            public int Length;
            public ComponentDataArray<SteeringComponentData> steering;
            public ComponentArray<Transform> transform;

            ComponentDataArray<AsteroidTagComponentData> _tag;
        }

        [InjectComponentGroup]
        Asteroids asteroids;

        int displacement;

        override protected void OnUpdate()
        {
            if (asteroids.Length <= 0)
                return;

            displacement++;
            float dt = Time.deltaTime;

            for (int i = 0; i < asteroids.transform.Length; i++)
            {
                var pos = asteroids.transform[i].position;

                asteroids.transform[i].position = new Vector3(pos.x + asteroids.steering[i].dx, pos.y + asteroids.steering[i].dy, 0);
                asteroids.transform[i].rotation = Quaternion.Euler(0f, 0f, asteroids.steering[i].angle + displacement);
            }
        }
    }
}