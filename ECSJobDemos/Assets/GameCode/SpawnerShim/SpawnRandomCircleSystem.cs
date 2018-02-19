using System.Collections.Generic;
using Unity.Collections;
using Unity.ECS;
using Unity.Mathematics;
using UnityEngine.ECS.SpawnerShim;
using Unity.Transforms;
using UnityEngine.ECS.Utilities;

namespace ECS.Spawners
{
    [DisableSystemWhenEmpty]
    public class SpawnRandomCircleSystem : ComponentSystem
    {
        struct SpawnRandomCircleInstance
        {
            public int spawnerIndex;
            public Entity sourceEntity;
            public float3 position;
            public float radius;
        }

        ComponentGroup m_MainGroup;

        protected override void OnCreateManager(int capacity)
        {
            m_MainGroup = GetComponentGroup(typeof(SpawnRandomCircle),typeof(Position));
        }

        protected override void OnUpdate()
        {
            var uniqueTypes = new List<SpawnRandomCircle>(10);

            EntityManager.GetAllUniqueSharedComponentDatas(uniqueTypes);

            int spawnInstanceCount = 0;
            for (int sharedIndex = 0; sharedIndex != uniqueTypes.Count; sharedIndex++)
            {
                var spawner = uniqueTypes[sharedIndex];
                var group = m_MainGroup.GetVariation(spawner);
                var entities = group.GetEntityArray();
                spawnInstanceCount += entities.Length;
                group.Dispose();
            }

            if (spawnInstanceCount == 0)
            {
                return;
            }

            var spawnInstances = new NativeArray<SpawnRandomCircleInstance>(spawnInstanceCount, Allocator.Temp);
            {
                int spawnIndex = 0;
                for (int sharedIndex = 0; sharedIndex != uniqueTypes.Count; sharedIndex++)
                {
                    var spawner = uniqueTypes[sharedIndex];
                    var group = m_MainGroup.GetVariation(spawner);
                    var entities = group.GetEntityArray();
                    var positions = group.GetComponentDataArray<Position>();

                    for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
                    {
                        var spawnInstance = new SpawnRandomCircleInstance();

                        spawnInstance.sourceEntity = entities[entityIndex];
                        spawnInstance.spawnerIndex = sharedIndex;
                        spawnInstance.position = positions[entityIndex].Value;

                        spawnInstances[spawnIndex] = spawnInstance;
                        spawnIndex++;
                    }

                    group.Dispose();
                }
            }

            for (int spawnIndex = 0; spawnIndex < spawnInstances.Length; spawnIndex++)
            {
                int spawnerIndex = spawnInstances[spawnIndex].spawnerIndex;
                var spawner = uniqueTypes[spawnerIndex];
                int count = spawner.count;
                var entities = new NativeArray<Entity>(count,Allocator.Temp);
                var positions = new NativeArray<float3>(count,Allocator.Temp);
                var prefab = spawner.prefab;
                float radius = spawner.radius;
                float3 center = spawnInstances[spawnIndex].position;
                var sourceEntity = spawnInstances[spawnIndex].sourceEntity;

                EntityManager.Instantiate(prefab, entities);

                if (spawner.spawnLocal)
                {
                    GeneratePoints.RandomPointsOnCircle(new float3(),radius, ref positions);
                    for (int i = 0; i < count; i++)
                    {
                        var position = new LocalPosition
                        {
                            Value = positions[i]
                        };
                        EntityManager.SetComponentData(entities[i],position);
                        EntityManager.AddComponentData(entities[i], new TransformParent { Value = sourceEntity});
                    }
                }
                else
                {
                    GeneratePoints.RandomPointsOnCircle(center,radius,ref positions);
                    for (int i = 0; i < count; i++)
                    {
                        var position = new Position
                        {
                            Value = positions[i]
                        };
                        EntityManager.SetComponentData(entities[i],position);
                    }
                }

                EntityManager.RemoveComponent<SpawnRandomCircle>(sourceEntity);

                entities.Dispose();
                positions.Dispose();
            }
            spawnInstances.Dispose();
        }
    }
}
