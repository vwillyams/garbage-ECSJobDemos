using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Experimental.AI;

public struct CrowdAgentNavigator : IComponentData
{
    public float3 requestedDestination;
    public NavMeshLocation requestedDestinationLocation;
    public float distanceToDestination; // TODO: make sure this is the path distance, not euclidean distance [#adriant]
    public float speed;
    public float nextCornerSide;
    public float3 steeringTarget;
    public bool newDestinationRequested;
    public bool goToDestination;
    public bool destinationInView;
    public bool destinationReached;
    public bool active;

    public void MoveTo(float3 dest)
    {
        requestedDestination = dest;
        newDestinationRequested = true;
    }

    public void StartMoving()
    {
        goToDestination = true;
        destinationInView = false;
        destinationReached = false;
        distanceToDestination = -1f;
    }
}

public class CrowdAgentNavigatorComponent : ComponentDataWrapper<CrowdAgentNavigator> {}
