using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Spawners;
using UnityEngine.ECS.Transform;
using UnityEngine.Jobs;

namespace ECS.Spawners
{
    public class SpawnRandomCircleSystem : ComponentSystem
    {
        struct SpawnRandomCircleInstance
        {
            public SpawnRandomCircle spawner;
            public Entity sourceEntity;
            public float3 position;
            public float radius;
        }
    
        protected override void OnUpdate()
        {
            var uniqueTypes = new List<SpawnRandomCircle>(10);
            var maingroup = EntityManager.CreateComponentGroup(typeof(SpawnRandomCircle));
            maingroup.CompleteDependency();

            EntityManager.GetAllUniqueSharedComponents(uniqueTypes);

            var spawnInstances = new List<SpawnRandomCircleInstance>(10);

            for (int sharedIndex = 0; sharedIndex != uniqueTypes.Count;sharedIndex++)
            {
                var spawner = uniqueTypes[sharedIndex];
                var group = maingroup.GetVariation(spawner);
                var entities = group.GetEntityArray();
                // var entityTransforms = group.GetTransformAccessArray();

                for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
                {
                    // var entityTransform = EntityManager.GetComponent<TransformAccess>(entities[entityIndex]);
                    // var entityPosition = entityTransforms[entityIndex].position;
                    var entityPosition = new float3(0.0f,0.0f,0.0f);
                    
                    var spawnInstance  = new SpawnRandomCircleInstance();

                    spawnInstance.sourceEntity = entities[entityIndex];
                    spawnInstance.spawner = spawner;
                    spawnInstance.position = new float3( entityPosition.x, entityPosition.y, entityPosition.z );
                    
                    spawnInstances.Add(spawnInstance);
                }

                group.Dispose();
            }
            maingroup.Dispose();

            for (int spawnIndex = 0; spawnIndex < spawnInstances.Count; spawnIndex++)
            {
                int count = spawnInstances[spawnIndex].spawner.count;
                var entities = new NativeArray<Entity>(count,Allocator.Temp);
                var prefab = spawnInstances[spawnIndex].spawner.prefab;
                float radius = spawnInstances[spawnIndex].spawner.radius;

                EntityManager.Instantiate(prefab, entities);
                if (!EntityManager.HasComponent<TransformPosition>(entities[0]))
                {
                    var position = new TransformPosition();
                    for (int i = 0; i < count; i++)
                    {
                        EntityManager.AddComponent(entities[i],position);
                    }
                }
                if (!EntityManager.HasComponent<TransformMatrix>(entities[0]))
                {
                    var position = new TransformMatrix();
                    for (int i = 0; i < count; i++)
                    {
                        EntityManager.AddComponent(entities[i],position);
                    }
                }
                
                for (int i = 0; i < count; i++)
                {
                    float angle = Random.Range(0.0f, Mathf.PI * 2.0f);
                    float x = math.sin(angle) * radius;
                    float z = math.cos(angle) * radius;
                    var position = new TransformPosition();
                    position.position = new float3(x,0.0f,z);
                    EntityManager.SetComponent(entities[i],position);
                }
                
                EntityManager.RemoveSharedComponent<SpawnRandomCircle>(spawnInstances[spawnIndex].sourceEntity);
                
                entities.Dispose();
            }
        } 
    }
}
