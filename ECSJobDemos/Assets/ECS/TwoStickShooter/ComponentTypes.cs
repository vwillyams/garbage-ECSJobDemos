﻿using Unity.Mathematics;
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

    public struct ShotSpawnData : IComponentData
    {
        public Shot Shot;
        public Transform2D Transform;
    }
}
