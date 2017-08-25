using UnityEngine.ECS;
using UnityEngine;
using UnityEngine.Experimental.AI;

public struct CrowdAgent : IComponentData
{
    public float3 worldPosition;
    public float3 velocity;
    public NavMeshLocation location;
}

public class CrowdAgentComponent : ComponentDataWrapper<CrowdAgent>
{
    protected override void OnEnable()
    {
        base.OnEnable();
        var agent = new CrowdAgent { worldPosition = transform.position };
        Value = agent;
    }
}
