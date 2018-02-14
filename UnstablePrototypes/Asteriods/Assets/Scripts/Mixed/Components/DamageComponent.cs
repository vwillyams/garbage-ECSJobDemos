using UnityEngine;
using Unity.ECS;

public struct DamageCompmonentData : IComponentData
{
    public int amount;
    public DamageCompmonentData(int amount)
    {
        this.amount = amount;
    }
}