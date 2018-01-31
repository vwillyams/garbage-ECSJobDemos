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
    public class SpawnRandomCircleSystem : ComponentSystem
    {
        struct SpawnRandomCircleInstance
        {
            public int spawnerIndex;
            public Entity sourceEntity;
            public float3 position;
            public float radius;
        }

        protected override void OnUpdate()
        {
            var uniqueTypes = new List<SpawnRandomCircle>(10);
            var maingroup = EntityManager.CreateComponentGroup(typeof(SpawnRandomCircle),typeof(TransformPosition));
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

            var spawnInstances = new NativeArray<SpawnRandomCircleInstance>(spawnInstanceCount, Allocator.Temp);
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
                        var spawnInstance = new SpawnRandomCircleInstance();

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
                float3 center = spawnInstances[spawnIndex].position;
                var sourceEntity = spawnInstances[spawnIndex].sourceEntity;

                EntityManager.Instantiate(prefab, entities);
                
                for (int i = 0; i < count; i++)
                {
                    float angle = Random.Range(0.0f, Mathf.PI * 2.0f);
                    float x = math.sin(angle) * radius;
                    float z = math.cos(angle) * radius;
                    var position = new TransformPosition();
                    position.position = center + (new float3(x,0.0f,z));
                    EntityManager.SetComponent(entities[i],position);
                }

                EntityManager.RemoveSharedComponent<SpawnRandomCircle>(sourceEntity);
                
                entities.Dispose();
            }
            spawnInstances.Dispose();
        } 
    }
}
