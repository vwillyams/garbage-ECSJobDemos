using Unity.Mathematics;
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
    public BlittableBool newDestinationRequested;
    public BlittableBool goToDestination;
    public BlittableBool destinationInView;
    public BlittableBool destinationReached;
    public BlittableBool active;

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
