using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleMovement;
using UnityEngine.ECS.Spawners;
using UnityEngine.ECS.Transform;

namespace ECS.Spawners
{
    public class SpawnRandomInSphereSystem : ComponentSystem
    {
        struct SpawnRandomInSphereInstance
        {
            public int spawnerIndex;
            public Entity sourceEntity;
            public float3 position;
            public float radius;
        }

        protected override void OnUpdate()
        {
            var uniqueTypes = new List<SpawnRandomInSphere>(10);
            var maingroup = EntityManager.CreateComponentGroup(typeof(SpawnRandomInSphere),typeof(TransformPosition));
            maingroup.CompleteDependency();

            EntityManager.GetAllUniqueSharedComponents(uniqueTypes);

            int spawnInstanceCount = 0;
            for (int sharedIndex = 0; sharedIndex != uniqueTypes.Count; sharedIndex++)
            {
                var spawner = uniqueTypes[sharedIndex];
                var group = maingroup.GetVariation(spawner);
                var entities = group.GetEntityArray();
                spawnInstanceCount += entities.Length;
                group.Dispose();
            }
            
            if (spawnInstanceCount == 0)
            {
                maingroup.Dispose();
                return;
            }

            var spawnInstances = new NativeArray<SpawnRandomInSphereInstance>(spawnInstanceCount, Allocator.Temp);
            {
                int spawnIndex = 0;
                for (int sharedIndex = 0; sharedIndex != uniqueTypes.Count; sharedIndex++)
                {
                    var spawner = uniqueTypes[sharedIndex];
                    var group = maingroup.GetVariation(spawner);
                    var entities = group.GetEntityArray();
                    var positions = group.GetComponentDataArray<TransformPosition>();

                    for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
                    {
                        var spawnInstance = new SpawnRandomInSphereInstance();

                        spawnInstance.sourceEntity = entities[entityIndex];
                        spawnInstance.spawnerIndex = sharedIndex;
                        spawnInstance.position = positions[entityIndex].position;

                        spawnInstances[spawnIndex] = spawnInstance;
                        spawnIndex++;
                    }

                    group.Dispose();
                }
            }
            
            maingroup.Dispose();

            for (int spawnIndex = 0; spawnIndex < spawnInstances.Length; spawnIndex++)
            {
                int spawnerIndex = spawnInstances[spawnIndex].spawnerIndex;
                var spawner = uniqueTypes[spawnerIndex];
                int count = spawner.count;
                var entities = new NativeArray<Entity>(count,Allocator.Temp);
                var prefab = spawner.prefab;
                float radius = spawner.radius;
                float radiusSquared = radius*radius;
                var spawnPositions = new NativeArray<float3>(count, Allocator.Temp);
                float3 center = spawnInstances[spawnIndex].position;
                var sourceEntity = spawnInstances[spawnIndex].sourceEntity;
                
                var spawnPositionsFound = 0;
                while (spawnPositionsFound < count)
                {
                    float x = Random.Range(-radius, radius);
                    float y = Random.Range(-radius, radius);
                    float z = Random.Range(-radius, radius);
                    if (((x * x) + (y * y) + (z * z)) < radiusSquared)
                    {
                        spawnPositions[spawnPositionsFound] = new float3(x,y,z);
                        spawnPositionsFound++;
                    }
                }

                EntityManager.Instantiate(prefab, entities);
                
                for (int i = 0; i < count; i++)
                {
                    var position = new TransformPosition
                    {
                        position = center + spawnPositions[i]
                    };
                    EntityManager.SetComponent(entities[i],position);
                }

                EntityManager.RemoveSharedComponent<SpawnRandomInSphere>(sourceEntity);
                
                spawnPositions.Dispose();
                entities.Dispose();
            }
            spawnInstances.Dispose();
        } 
    }
}
