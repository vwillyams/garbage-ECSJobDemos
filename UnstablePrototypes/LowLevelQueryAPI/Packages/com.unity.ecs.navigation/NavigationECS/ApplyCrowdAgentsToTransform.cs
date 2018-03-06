using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Entities;

[UpdateAfter(typeof(CrowdSystem))]
public class CrowdAgentsToTransformSystem : JobComponentSystem
{
    struct CrowdGroup
    {
        [ReadOnly]
        public ComponentDataArray<CrowdAgent> agents;

        public TransformAccessArray agentTransforms;

        [ReadOnly]
        public ComponentDataArray<WriteToTransformMarker> m_WriteToTrasformFlag;
    }

    [Inject]
    CrowdGroup m_Crowd;

    struct WriteCrowdAgentsToTransformsJob : IJobParallelForTransform
    {
        [ReadOnly]
        public ComponentDataArray<CrowdAgent> crowdAgents;

        public void Execute(int index, TransformAccess transform)
        {
            var agent = crowdAgents[index];
            transform.position = agent.worldPosition;
            if (math.length(agent.velocity) > 0.1f)
                transform.rotation = Quaternion.LookRotation(agent.velocity);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        WriteCrowdAgentsToTransformsJob writeJob;
        writeJob.crowdAgents = m_Crowd.agents;
        return writeJob.Schedule(m_Crowd.agentTransforms, inputDeps);
    }
}
