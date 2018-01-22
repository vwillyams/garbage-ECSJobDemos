using UnityEngine;
using UnityEngine.ECS;

namespace Asteriods.Client
{
    public class InputSystem : ComponentSystem
    {
        struct Player
        {
            public int Length;
            public ComponentDataArray<PlayerInputComponentData> inputs;
            public ComponentDataArray<ShipInfoComponentData> ship;
            ComponentDataArray<PlayerTagComponentData> tags;
        }

        [InjectComponentGroup]
        Player player;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
        }

        override protected void OnUpdate()
        {
            if (player.Length == 0)
                return;

            Debug.Assert(player.Length == 1);

            byte left = 0;
            byte right = 0;
            byte thrust = 0;
            byte shoot = 0;

            if (Input.GetKey("left"))
                left = 1;
            if (Input.GetKey("right"))
                right = 1;
            if (Input.GetKey("up"))
                thrust = 1;
            if (Input.GetKeyDown("space"))
            //if (Input.GetKey("space"))
                shoot = 1;

            //Debug.LogFormat("left {0}, right {1}, up {2}, space {3}", left, right, thrust, shoot);

            player.inputs[0] = new PlayerInputComponentData(left, right, thrust, shoot);
            if (player.ship[0].entity != Entity.Null)
                EntityManager.SetComponent<ShipStateComponentData>(player.ship[0].entity, new ShipStateComponentData(thrust));
        }
    }

}
