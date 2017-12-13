using UnityEngine;
using UnityEngine.ECS;

public struct PlayerInputComponentData : IComponentData
{
    public byte left;
    public byte right;
    public byte thrust;
    public byte shoot;

    public PlayerInputComponentData(byte left, byte right, byte thrust, byte shoot)
    {
        this.left = left;
        this.right = right;
        this.thrust = thrust;
        this.shoot = shoot;
    }
}

