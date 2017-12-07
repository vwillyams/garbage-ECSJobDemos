using UnityEngine;
using UnityEngine.ECS;

public struct DamageCompmonentData : IComponentData
{
    public int amount;
    public DamageCompmonentData(int amount)
    {
        this.amount = amount;
    }
}