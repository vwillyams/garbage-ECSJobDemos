using UnityEngine;
using UnityEngine.ECS;

public struct BulletTagComponentData : IComponentData
{
}

public class BulletTagComponent: ComponentDataWrapper<BulletTagComponentData> { }