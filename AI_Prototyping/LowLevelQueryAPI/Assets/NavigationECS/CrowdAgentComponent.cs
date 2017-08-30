using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Experimental.AI;

public struct CrowdAgent : IComponentData
{
    public int type;    // NavMeshAgent type
    public float3 worldPosition;
    public float3 velocity;
    public NavMeshLocation location;
}

public class CrowdAgentComponent : ComponentDataWrapper<CrowdAgent>
{
    protected override void OnEnable()
    {
        base.OnEnable();
        var agent = new CrowdAgent { type = 0, worldPosition = transform.position };
        Value = agent;
    }
}
