using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using UnityEngine.ECS;

[UpdateAfter(typeof(CrowdSystem))]
class CrowdAgentsToTransformSystem : JobComponentSystem
{
    [InjectTuples]
    ComponentDataArray<CrowdAgent> m_CrowdAgents;

    [InjectTuples]
    TransformAccessArray m_CrowdAgentTransforms;

    [InjectTuples]
    ComponentDataArray<WriteToTransformMarker> m_WriteToTrasformFlag;

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

    override public void OnUpdate()
    {
        base.OnUpdate();

        WriteCrowdAgentsToTransformsJob writeJob;
        writeJob.crowdAgents = m_CrowdAgents;
        AddDependency(writeJob.Schedule(m_CrowdAgentTransforms, GetDependency()));
    }
}
