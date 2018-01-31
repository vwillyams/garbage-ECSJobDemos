using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickExample
{
    public struct Transform2D : IComponentData
    {
        public float2 Position;
        public float2 Heading;
    }

    public struct PlayerInput : IComponentData
    {
        public float2 Move;
        public float2 Shoot;
        public float FireCooldown;

        public bool Fire => FireCooldown <= 0.0 && math.length(Shoot) > 0.5f;
    }

    public struct Shot : IComponentData
    {
        public float Speed;
        public float TimeToLive;
    }

    // A tag type added to shot entities created by the player, so we can segregate them
    public struct PlayerShot : ISharedComponentData
    {
    }

    public struct EnemyShot : ISharedComponentData
    {
    }

    public struct ShotSpawnData : IComponentData
    {
        public Shot Shot;
        public Transform2D Transform;
        public byte IsPlayer;
    }

    public struct Enemy : IComponentData
    {
        public int Health;
    }

    public struct EnemyShootState : IComponentData
    {
        public float Cooldown;
    }

    // TODO: Call out that this is better than storing state in the system, because it can support things like replay.
    public struct EnemySpawnSystemState : IComponentData
    {
        public int SpawnedEnemyCount;
        public float Cooldown;
        public Random.State RandomState;
    }
}
