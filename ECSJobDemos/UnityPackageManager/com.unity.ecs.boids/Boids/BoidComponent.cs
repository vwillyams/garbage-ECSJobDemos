﻿using Unity.ECS;

namespace UnityEngine.ECS.Boids
{
    public struct Boid : IComponentData { }

    public class BoidComponent : ComponentDataWrapper<Boid> { }
}
