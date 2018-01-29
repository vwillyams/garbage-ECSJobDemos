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
        public float FireCooldown;
        public byte Fire;
    }

    public struct Shot : IComponentData
    {
        public float Speed;
        public float TimeToLive;
    }

    public struct ShotSpawnData : IComponentData
    {
        public Shot Shot;
        public WorldPos WorldPos;
    }
}
