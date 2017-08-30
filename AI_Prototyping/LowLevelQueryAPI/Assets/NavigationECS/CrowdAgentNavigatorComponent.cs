using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Experimental.AI;

public struct CrowdAgentNavigator : IComponentData
{
    public int crowdId;
    public float3 requestedDestination;
    public NavMeshLocation requestedDestinationLocation;
    public float distanceToDestination; // TODO: make sure this is the path distance, not euclidean distance [#adriant]
    public bool newDestinationRequested;
    public bool goToDestination;
    public bool destinationInView;
    public bool destinationReached;
    public bool active;

    public void MoveTo(float3 dest)
    {
        requestedDestination = dest;
        newDestinationRequested = true;
        goToDestination = true;
        destinationInView = false;
        destinationReached = false;
        distanceToDestination = -1f;
    }
}

public class CrowdAgentNavigatorComponent : ComponentDataWrapper<CrowdAgentNavigator>
{
    protected override void OnEnable()
    {
        base.OnEnable();
        var agentNavigator = new CrowdAgentNavigator
        {
            active = true,
            crowdId = -1,
            newDestinationRequested = false,
            goToDestination = false,
            destinationInView = false,
            destinationReached = true
        };
        Value = agentNavigator;
    }
}
