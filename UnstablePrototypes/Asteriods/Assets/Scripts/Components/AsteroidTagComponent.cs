using UnityEngine;
using UnityEngine.ECS;

public struct AsteroidTagComponentData : IComponentData
{
}

public class AsteroidTagComponent: ComponentDataWrapper<AsteroidTagComponentData> { }