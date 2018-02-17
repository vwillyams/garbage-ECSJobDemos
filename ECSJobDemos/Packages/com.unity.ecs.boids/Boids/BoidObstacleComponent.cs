using Unity.ECS;

namespace UnityEngine.ECS.Boids
{
	public struct BoidObstacle : IComponentData { }

	public class BoidObstacleComponent : ComponentDataWrapper<BoidObstacle> { }
}