using UnityEngine;
using UnityEngine.ECS;

public class InputSystem : ComponentSystem
{
    struct Player
    {
        public ComponentDataArray<PlayerTagComponentData> self;
        public ComponentDataArray<PlayerInputComponentData> input;
    }


    [InjectComponentGroup]
    Player player;

    override protected void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);
    }

    override protected void OnUpdate()
    {
        if (player.self.Length == 0)
            return;

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
        //if (Input.GetKeyDown("space"))
        if (Input.GetKey("space"))
            shoot = 1;

        player.input[0] = new PlayerInputComponentData(left, right, thrust, shoot);
    }
}