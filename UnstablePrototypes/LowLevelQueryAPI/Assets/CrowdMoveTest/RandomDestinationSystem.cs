using System;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Entities;
using UnityEngine.Experimental.AI;

[UpdateAfter(typeof(CrowdSystem))]
public class RandomDestinationSystem : JobComponentSystem
{
    struct CrowdGroup
    {
        [ReadOnly]
        public ComponentDataArray<CrowdAgent> agents;
        public ComponentDataArray<CrowdAgentNavigator> agentNavigators;
    }

    [Inject]
    CrowdGroup m_Crowd;

    NavMeshQuery m_NavMeshQuery;

    const int k_AgentsPerBatch = 100;

    protected override void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);

        m_NavMeshQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent);
    }

    protected override void OnDestroyManager()
    {
        base.OnDestroyManager();

        m_NavMeshQuery.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (m_Crowd.agents.Length == 0)
            return inputDeps;

        var destinationJob = new SetDestinationJob { query = m_NavMeshQuery, agents = m_Crowd.agents, agentNavigators = m_Crowd.agentNavigators, randomNumber = UnityEngine.Random.value };
        return destinationJob.Schedule(m_Crowd.agents.Length, k_AgentsPerBatch, inputDeps);
    }

    public struct SetDestinationJob : IJobParallelFor
    {
        [ReadOnly]
        public NavMeshQuery query;
        [ReadOnly]
        public float randomNumber;
        [ReadOnly]
        public ComponentDataArray<CrowdAgent> agents;

        public ComponentDataArray<CrowdAgentNavigator> agentNavigators;

        public void Execute(int index)
        {
            if (index >= agents.Length)
                return;

            var agentNavigator = agentNavigators[index];
            if (!agentNavigator.active || !agentNavigator.destinationReached)
                return;

            var agent = agents[index];
            if (!query.IsValid(agent.location))
                return;

            var agPos = agent.location.position;
            var agVel = agent.velocity;
            var randomAngle = ((agPos.x + agPos.y + agPos.z + agVel.x + agVel.y + agVel.z + randomNumber) * (1 + index) % 2f) * 360f;
            var heading = Quaternion.Euler(0, randomAngle, 0) * Vector3.forward;
            var dist = Mathf.Abs(randomAngle) % 10f;
            var dest = agent.location.position + dist * heading;

            agentNavigator.MoveTo(dest);
            agentNavigator.speed = 3f;
            agentNavigators[index] = agentNavigator;
        }
    }
}
