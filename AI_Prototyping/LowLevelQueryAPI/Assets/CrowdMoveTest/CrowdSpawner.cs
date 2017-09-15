using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

public class CrowdSpawner : MonoBehaviour
{
    public int newAgents = 16;
    public float range = 11.2f;
    public GameObject prefab;

    public int totalAgentCount = 0;

    void Update()
    {
		var entityManager = DependencyManager.GetBehaviourManager<EntityManager>();
        for (int i = 0; i < newAgents; ++i)
        {
            var pos = new Vector3(Random.Range(-range, range), 0, Random.Range(-range, range));
            var go = GameObject.Instantiate(prefab, pos, Quaternion.identity, transform) as GameObject;
            go.name = "CrowdAgent_" + (totalAgentCount + i);
            var agent = new CrowdAgent { type = 0, worldPosition = pos };
            if (entityManager == null)
            {
                Debug.Log("No ent man");
                return;
            }
            entityManager.SetComponent<CrowdAgent>(go.GetComponent<GameObjectEntity>().Entity, agent);
            var agentNavigator = new CrowdAgentNavigator
            {
                active = true,
                crowdId = -1,
                newDestinationRequested = false,
                goToDestination = false,
                destinationInView = false,
                destinationReached = true
            };
            entityManager.SetComponent<CrowdAgentNavigator>(go.GetComponent<GameObjectEntity>().Entity, agentNavigator);
        }
        totalAgentCount += newAgents;
        newAgents = 0;
    }
}
