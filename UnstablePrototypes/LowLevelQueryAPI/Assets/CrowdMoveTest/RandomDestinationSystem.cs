﻿using System;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.ECS;

[UpdateAfter(typeof(CrowdSystem))]
public class RandomDestinationSystem : JobComponentSystem
{
    struct CrowdGroup
    {
        [ReadOnly]
        public ComponentDataArray<CrowdAgent> agents;
        public ComponentDataArray<CrowdAgentNavigator> agentNavigators;
    }

    [InjectComponentGroup]
    CrowdGroup m_Crowd;

    const int k_AgentsPerBatch = 100;

    protected override void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);
    }

    protected override void OnDestroyManager()
    {
        base.OnDestroyManager();
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        if (m_Crowd.agents.Length == 0)
            return;

        CompleteDependency();

        var destinationJob = new SetDestinationJob { agents = m_Crowd.agents, agentNavigators = m_Crowd.agentNavigators, randomNumber = UnityEngine.Random.value };
        var afterDestinationsSet = destinationJob.Schedule(m_Crowd.agents.Length, k_AgentsPerBatch);
        JobHandle.ScheduleBatchedJobs();

        AddDependency(afterDestinationsSet);
    }

    public struct SetDestinationJob : IJobParallelFor
    {
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
            if (!agent.location.valid)
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
