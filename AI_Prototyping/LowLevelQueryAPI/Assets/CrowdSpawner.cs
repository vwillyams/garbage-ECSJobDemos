using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrowdSpawner : MonoBehaviour
{
    public int newAgents = 16;
    public float range = 11.2f;
    public GameObject prefab;

    public int totalAgentCount = 0;

    void Update()
    {
        for (int i = 0; i < newAgents; ++i)
        {
            var pos = new Vector3(Random.Range(-range, range), 0, Random.Range(-range, range));
            var go = GameObject.Instantiate(prefab, pos, Quaternion.identity, transform) as GameObject;
            go.name = "CrowdAgent_" + (totalAgentCount + i);
        }
        totalAgentCount += newAgents;
        newAgents = 0;
    }
}
