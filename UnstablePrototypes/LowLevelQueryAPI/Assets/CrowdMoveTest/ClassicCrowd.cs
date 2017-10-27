using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

public class ClassicCrowd : MonoBehaviour
{
    public GameObject agentPrefab;
    public int agentSpawnCount = 600;
    public int agentsCheckedPerFrame = 30;
    [Range(0.01f, 3.0f)]
    public float agentStoppingDistance = 1.1f;
    public bool waitForAllToArrive = false;
    public bool drawDebug = false;

    int m_Count;
    NativeArray<Vector3> m_Positions;
    List<NavMeshAgent> m_NavMeshAgents;
    int m_AgentIdx;
    List<int> m_RoundCompleted;
    int m_Round;
    bool m_WaitForAllToArriveOld;
    float m_AgentStoppingDistanceOld = Mathf.Infinity;

    void OnEnable()
    {
        m_Count = agentSpawnCount;
        m_NavMeshAgents = new List<NavMeshAgent>(m_Count);
        m_RoundCompleted = new List<int>(m_Count);
        m_Positions = new NativeArray<Vector3>(m_Count, Allocator.Persistent);
        for (var i = 0; i < m_Count; ++i)
        {
            m_Positions[i] = new Vector3(Random.Range(-9.5f, 9.5f), 0, Random.Range(-9.5f, 9.5f));
        }
    }

    void OnDisable()
    {
        m_Positions.Dispose();
        m_NavMeshAgents.Clear();
        m_RoundCompleted.Clear();
    }

    void Start()
    {
        for (var i = 0; i < m_Count; ++i)
        {
            var go = Instantiate(agentPrefab, m_Positions[i], Quaternion.identity);
            go.name = "ClassicAgent" + i;
            var agent = go.GetComponent<NavMeshAgent>();
            Assert.IsNotNull(agent);
            m_NavMeshAgents.Add(agent);
        }

        ResetRound();
    }

    void Update()
    {
        if (waitForAllToArrive != m_WaitForAllToArriveOld)
        {
            ResetRound();
        }

        if (waitForAllToArrive)
        {
            var allCompletedRound = m_RoundCompleted.All(t => t >= m_Round);
            if (allCompletedRound)
                m_Round += 2;
        }

        if (agentsCheckedPerFrame > m_Count)
            agentsCheckedPerFrame = m_Count;

        if (m_AgentStoppingDistanceOld != agentStoppingDistance)
        {
            foreach (var agent in m_NavMeshAgents)
            {
                agent.stoppingDistance = agentStoppingDistance;
            }
            m_AgentStoppingDistanceOld = agentStoppingDistance;
        }

        var countTop = m_AgentIdx + agentsCheckedPerFrame;
        while (countTop > 0)
        {
            var maxIdx = Math.Min(countTop, m_Count);
            for (; m_AgentIdx < maxIdx; ++m_AgentIdx)
            {
                var agent = m_NavMeshAgents[m_AgentIdx];

                var needsReplan = !agent.pathPending && agent.remainingDistance < agentStoppingDistance && (Math.Abs(agent.velocity.sqrMagnitude) < 0.01f || !agent.hasPath);
                if (needsReplan)
                {
                    var allowedToReplan = !waitForAllToArrive || m_RoundCompleted[m_AgentIdx] < m_Round - 1;
                    if (allowedToReplan)
                    {
                        m_Positions[m_AgentIdx] = new Vector3(Random.Range(-9.5f, 9.5f), 0, Random.Range(-9.5f, 9.5f));
                        agent.destination = m_Positions[m_AgentIdx];

                        if (waitForAllToArrive)
                            m_RoundCompleted[m_AgentIdx]++;

                        if (drawDebug)
                            Debug.DrawLine(agent.transform.position + Vector3.up, m_Positions[m_AgentIdx], Color.white);
                    }
                    else
                    {
                        m_RoundCompleted[m_AgentIdx] = m_Round;
                    }
                }
            }
            if (m_AgentIdx >= m_Count)
                m_AgentIdx = 0;

            countTop -= m_Count;
        }
        if (drawDebug)
        {
            foreach (var agent in m_NavMeshAgents)
            {
                if (agent.pathPending)
                {
                    Debug.DrawRay(agent.transform.position + Vector3.up, Vector3.up, Color.red);
                }
                else if (waitForAllToArrive && (m_RoundCompleted[m_AgentIdx] >= m_Round - 1) && Math.Abs(agent.velocity.sqrMagnitude) < 0.01f)
                {
                    Debug.DrawRay(agent.transform.position + 0.2f * Vector3.up, Vector3.up, Color.yellow);
                }

                //Debug.DrawRay(agent.transform.position + Vector3.up, agent.velocity, Color.grey);
            }
        }
    }

    void ResetRound()
    {
        m_Round = 0;
        m_RoundCompleted.Clear();
        if (waitForAllToArrive)
        {
            for (var i = 0; i < m_NavMeshAgents.Count; ++i)
            {
                m_RoundCompleted.Add(0);
            }
        }
        m_WaitForAllToArriveOld = waitForAllToArrive;
    }
}
