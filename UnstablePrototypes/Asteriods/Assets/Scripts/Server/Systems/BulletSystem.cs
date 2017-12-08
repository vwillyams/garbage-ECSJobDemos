using UnityEngine;
using UnityEngine.ECS;

using Unity.Mathematics;

namespace Asteriods.Server
{
    public class BulletSystem : ComponentSystem
    {
        static float force = 0.1f;
        struct Player
        {
            public ComponentDataArray<PlayerTagComponentData> self;
            public ComponentDataArray<PlayerInputComponentData> input;
            public ComponentDataArray<VelocityComponentData> steering;
            public ComponentArray<Transform> transform;
        }

        struct Bullet
        {
            public ComponentDataArray<BulletTagComponentData> bullet;
            public ComponentDataArray<VelocityComponentData> velocity;
            public ComponentDataArray<RotationComponentData> rotation;
            public ComponentArray<Transform> transform;
        }


        [InjectComponentGroup]
        Player player;

        [InjectComponentGroup]
        Bullet bullets;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
        }

        override protected void OnUpdate()
        {

            if (bullets.transform.Length > 0)
            {
                for (int i = 0; i < bullets.transform.Length; i++)
                {
                    float angle = bullets.rotation[i].angle;
                    float dx = bullets.velocity[i].dx;
                    float dy = bullets.velocity[i].dy;

                    dy += math.sin(math.radians(angle + 90)) * force;
                    dx += math.cos(math.radians(angle + 90)) * force;

                    bullets.velocity[i] = new VelocityComponentData(dx, dy);
                    var pos = bullets.transform[i].position;

                    bullets.transform[i].position = new Vector3(pos.x + bullets.velocity[i].dx, pos.y + bullets.velocity[i].dy, 0);
                }
            }

            if (player.input.Length > 0)
            {
                PlayerInputComponentData input = player.input[0];

                if (input.shoot == 1)
                {
                    var a = player.transform[0].eulerAngles.z;
                    var p = player.transform[0].position;
                    var rotation = new RotationComponentData(a);
                    var obj = GameObject.Instantiate(GameSettings.Instance().bulletPrefab, new Vector3(p.x, p.y, 0), Quaternion.Euler(0f, 0f, rotation.angle));
                    EntityManager.AddComponent<RotationComponentData>(obj.GetComponent<GameObjectEntity>().Entity, rotation);

                    GameObject.Destroy(obj, 2f);
                }
            }
        }
    }
}