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
            public ComponentDataArray<VelocityComponentData> steering;
            public ComponentArray<Transform> transform;
            public ComponentDataArray<AsteroidTagComponentData> _tag;
        }

        [InjectComponentGroup]
        Asteroids asteroids;

        int displacement;

        override protected void OnUpdate()
        {
            if (asteroids.Length <= 0)
                return;

            float dt = Time.deltaTime;

            for (int i = 0; i < asteroids.transform.Length; i++)
            {
                var pos = asteroids.transform[i].position;

                asteroids.transform[i].position = new Vector3(pos.x + asteroids.steering[i].dx, pos.y + asteroids.steering[i].dy, 0);
                asteroids.transform[i].rotation = Quaternion.Euler(0f, 0f, asteroids.transform[i].rotation.eulerAngles.z + 2);
            }
        }
    }
}