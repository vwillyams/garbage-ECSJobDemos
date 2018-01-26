using Unity.Mathematics;
using UnityEngine.ECS;

namespace TwoStickExample
{
    public struct WorldPos : IComponentData
    {
        public float2 Position;
        public float2 Heading;
    }

    public struct PlayerInput : IComponentData
    {
        public float2 Move;
        public float2 Shoot;
        public byte Fire;
    }
}
