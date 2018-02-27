using Unity.Mathematics;
using Unity.ECS;
using UnityEngine.Experimental.AI;

public struct CrowdAgentNavigator : IComponentData
{
    public float3 requestedDestination;
    public NavMeshLocation requestedDestinationLocation;
    public float distanceToDestination; // TODO: make sure this is the path distance, not euclidean distance [#adriant]
    public NavMeshLocation pathStart;
    public NavMeshLocation pathEnd;
    public int pathSize;
    public float speed;
    public float nextCornerSide;
    public float3 steeringTarget;
    public bool1 newDestinationRequested;
    public bool1 goToDestination;
    public bool1 destinationInView;
    public bool1 destinationReached;
    public bool1 active;

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
