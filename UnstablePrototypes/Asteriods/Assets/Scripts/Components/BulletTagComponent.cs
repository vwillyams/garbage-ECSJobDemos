using UnityEngine;
using UnityEngine.ECS;

public struct BulletTagComponentData : IComponentData
{
}

public struct DamageCompmonentData : IComponentData
{
    public int amount;
    public DamageCompmonentData(int amount)
    {
        this.amount = amount;
    }
}

public class BulletTagComponent: ComponentDataWrapper<BulletTagComponentData> { }