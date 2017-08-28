using UnityEngine.ECS;
using UnityEngine;
using UnityEngine.Experimental.AI;

public struct CrowdAgent : IComponentData
{
    public int crowdId;
    public int type;    // NavMeshAgent type
    public float3 worldPosition;
    public float3 destination;
    public float3 velocity;
    public NavMeshLocation location;
    public NavMeshLocation destinationLocation;
    public float distanceToDestination; // TODO: make sure this is the path distance, not euclidean distance [#adriant]
    public bool goToDestination;
    public bool destinationReached;
    public bool active;

    public void MoveTo(float3 dest)
    {
        destination = dest;
        goToDestination = true;
        destinationReached = false;
        distanceToDestination = -1f;
    }
}

public class CrowdAgentComponent : ComponentDataWrapper<CrowdAgent>
{
    //public bool active
    //{
    //    get { return Value.active; }
    //    set
    //    {
    //        var agent = Value;
    //        agent.active = value;
    //        Value = agent;
    //    }
    //}

    protected override void OnEnable()
    {
        base.OnEnable();
        var agent = new CrowdAgent { active = true, type = 0, crowdId = -1, worldPosition = transform.position, goToDestination = false };
        Value = agent;
    }
}
