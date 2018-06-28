using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class SpawnRandomCircleSystem : ComponentSystem
{
	struct Group
	{
		[ReadOnly]
		public SharedComponentDataArray<Spawner> Spawner;
		public ComponentDataArray<Position> Position;
		public EntityArray Entity;
		public int Length;
	}

	[Inject] Group m_Group;


	protected override void OnUpdate()
	{
		while (m_Group.Length != 0)
		{
			var spawner = m_Group.Spawner[0];
			var entities = new NativeArray<Entity>(spawner.count, Allocator.Temp);
			var positions = new NativeArray<float3>(spawner.count, Allocator.Temp);
			try
			{
				var sourceEntity = m_Group.Entity[0];
				var center = m_Group.Position[0].Value;

				EntityManager.Instantiate(spawner.prefab, entities);
                
				if (spawner.spawnLocal)
				{
					RandomPointsInsideCircle(new float3(), spawner.radius, ref positions);
					for (int i = 0; i < spawner.count; i++)
					{
						var position = new LocalPosition
						{
							Value = positions[i]
						};
						EntityManager.SetComponentData(entities[i], position);
						EntityManager.AddComponentData(entities[i], new TransformParent { Value = sourceEntity });
					}
				}
				else
				{
					RandomPointsInsideCircle(center, spawner.radius, ref positions);
					for (int i = 0; i < spawner.count; i++)
					{
						var position = new Position
						{
							Value = positions[i]
						};
						EntityManager.SetComponentData(entities[i], position);
                        EntityManager.AddComponentData(entities[i], new ReturnToOrigin { origin = position.Value, returnForce = 10f });
					}
				}

				EntityManager.RemoveComponent<Spawner>(sourceEntity);

				// Instantiate & AddComponent & RemoveComponent calls invalidate the injected groups,
				// so before we get to the next spawner we have to reinject them  
				UpdateInjectedComponentGroups();
			}
			finally
			{
				entities.Dispose();
				positions.Dispose();
			}
		}
	}

    private void RandomPointsInsideCircle(float3 center, float radius, ref NativeArray<float3> points)
    {
        var count = points.Length;
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0.0f, Mathf.PI * 2.0f);
            points[i] = center + new float3
            {
                x = math.sin(angle) * Random.Range(0, radius),
                y = 0,
                z = math.cos(angle) * Random.Range(0, radius)
            };
        }
    }
}