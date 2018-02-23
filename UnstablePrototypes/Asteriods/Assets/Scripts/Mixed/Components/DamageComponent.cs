using UnityEngine;
using Unity.Entities;

public struct DamageCompmonentData : IComponentData
{
    public int amount;
    public DamageCompmonentData(int amount)
    {
        this.amount = amount;
    }
}
