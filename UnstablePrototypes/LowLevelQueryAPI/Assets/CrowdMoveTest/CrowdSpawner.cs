﻿using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Experimental.AI;

public class CrowdSpawner : MonoBehaviour
{
    public int newAgents = 16;
    public float range = 11.2f;
    public GameObject prefab;

    public int totalAgentCount = 0;

    const int k_MaxPathSize = 128;

    void Update()
    {
        var entityManager = DependencyManager.GetBehaviourManager<EntityManager>();
        for (var i = 0; i < newAgents; ++i)
        {
            var pos = new Vector3(Random.Range(-range, range), 0, Random.Range(-range, range));
            var go = Instantiate(prefab, pos, Quaternion.identity, transform);
            go.name = "CrowdAgent_" + (totalAgentCount + i);
            var entity = go.GetComponent<GameObjectEntity>().Entity;
            var agent = new CrowdAgent { type = 0, worldPosition = pos };
            entityManager.SetComponent(entity, agent);
            var agentNavigator = new CrowdAgentNavigator
            {
                active = true,
                newDestinationRequested = false,
                goToDestination = false,
                destinationInView = false,
                destinationReached = true
            };
            entityManager.SetComponent(entity, agentNavigator);
            entityManager.AddComponent(entity, ComponentType.FixedArray(typeof(PolygonID), k_MaxPathSize));
        }
        totalAgentCount += newAgents;
        newAgents = 0;
    }
}
