using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.ECS;
using UnityEngine;
using UnityEngine.Experimental.AI;

public struct CrowdAgent : IComponentData
{
    public float3 position;
    public float3 velocity;
    public NavMeshLocation location;
    public PathQueryQueue.Handle requestHandle;
}

public class CrowdAgentComponent : ComponentDataWrapper<CrowdAgent>
{
    protected override void OnEnable()
    {
        base.OnEnable();
        var agent = new CrowdAgent();
        agent.position = transform.position;
        Value = agent;
    }
}
