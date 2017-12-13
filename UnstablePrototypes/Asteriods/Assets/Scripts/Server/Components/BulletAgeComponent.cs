using UnityEngine;
using UnityEngine.ECS;

public struct BulletAgeComponentData : IComponentData
{
    public BulletAgeComponentData(float maxAge)
    {
        this.maxAge = maxAge;
        age = 0;
    }
    public float age;
    public float maxAge;
}
