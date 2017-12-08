using UnityEngine;
using UnityEngine.ECS;

[System.Serializable]
public struct CollisionSphereComponentData : IComponentData
{
    [SerializeField]
    public float radius;
}

public class CollisionSphereComponent: ComponentDataWrapper<CollisionSphereComponentData> { }