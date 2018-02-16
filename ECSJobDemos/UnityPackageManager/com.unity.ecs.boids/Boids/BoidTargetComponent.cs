using Unity.ECS;
using UnityEngine.ECS.Utilities;

namespace UnityEngine.ECS.Boids
{
	public struct BoidTarget : IComponentData { }

	public class BoidTargetComponent : ComponentDataWrapper<BoidTarget> { }
    
    public class BoidTargetFooSystem: FooSystem<BoidTarget> { }
}