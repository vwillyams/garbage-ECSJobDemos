using UnityEngine;
using UnityEngine.ECS;

namespace Asteriods.Client
{
    public class NetworkMessageSystem : ComponentSystem
    {
        struct SerializableData
        {
            public int Length;
            public ComponentDataArray<PlayerTagComponentData> self;
            public ComponentDataArray<PlayerInputComponentData> input;
        }

        [InjectComponentGroup]
        SerializableData data;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
        }

        override protected void OnDestroyManager()
        {
            base.OnDestroyManager();
        }

        override protected void OnUpdate()
        {
            for (int i = 0; i < data.Length; ++i)
            {
                if (data.input[i].left == 0 &&
                    data.input[i].right == 0 && 
                    data.input[i].shoot == 0 && 
                    data.input[i].thrust == 0)
                    continue;
                Debug.Log("input system passed to network message system");
                Asteriods.Server.NetworkEventSystem.inputEventQueue.Enqueue(data.input[0]);
            }
        }
    }
}