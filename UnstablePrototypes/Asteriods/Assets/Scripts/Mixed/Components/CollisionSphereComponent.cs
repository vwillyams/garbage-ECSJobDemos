using UnityEngine;
using Unity.Entities;

[System.Serializable]
public struct CollisionSphereComponentData : IComponentData
{
    [SerializeField]
    public float radius;

    public CollisionSphereComponentData(float radius)
    {
        this.radius = radius;
    }
}
