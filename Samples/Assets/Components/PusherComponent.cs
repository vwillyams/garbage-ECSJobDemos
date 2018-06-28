using System;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

[Serializable]
public struct Pusher : ISharedComponentData
{
    public float pushForce;
    public float3 position;
    public bool inverse;
    public bool alwaysActive;
}

public class PusherComponent : SharedComponentDataWrapper<Pusher> { }