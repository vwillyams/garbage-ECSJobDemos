This project is meant to work with code from the native branch scripting/jobsystem/unite-demo-2017 .\
Revision needed: **b9675b37b2ae** (2017-08-08 14:51:36 +0200)\
Good build (with ECS): http://beta.unity3d.com/download/b9675b37b2ae/download.html

Example scenes:

**CrowdMoveTest**

- showcases how to find paths between many source and destination points in parallel and time-sliced, and have entities move along simplified paths that go through the obtained polygons.
- With ECS: Add the CrowdAgentComponent to game objects and they will start moving on paths controlled by the CrowdSystem.
- Without ECS: _TheCrowd_ has _Crowd_ behaviour, written with the new API, where the number of entities can be specified.
    - It also hosts a disabled _Classic Crowd_ behaviour that can run an example of the old crowd simulation. The optional debug draw can show a red line above all agents that are waiting for the path to be computed or a short yellow line above the agents that have reached their destination and are currently waiting for new instructions.


**PathCorners**

- showcases how to find the corners on the most direct path between the Origin and Target game objects.
- _PathManager_ has the behaviour MultiplePaths where the Origins and Targets can be changed any time.
    - _Pathfind Iterations Per Update_ specifies the maximum number of NavMesh nodes that are traversed every frame. It serves as a way to delay the end result of pathfinding so that FindStraightPath() would have to wait one or several frames for the path to be ready.
- Origins and Targets don't have to contain the same number of elements
- the Origins and Targets game objects can be moved around at runtime in order to trigger changes in the path between them.

Exclusive API featured:

    namespace UnityEngine.Experimental.AI
    struct PolygonID
        PolygonID.polygon
        PolygonID.valid

    struct NavMeshLocation
        NavMeshLocation.polygon
        NavMeshLocation.position
        NavMeshLocation.valid
    
    enum PathQueryStatus
        PathQueryStatus.Failure
        PathQueryStatus.Success
        PathQueryStatus.InProgress

    enum NavMeshStraightPathFlags
        NavMeshStraightPathFlags.kStraightPathStart
        NavMeshStraightPathFlags.kStraightPathEnd
        NavMeshStraightPathFlags.kStraightPathOffMeshConnection

    enum NavMeshPolyTypes
        NavMeshPolyTypes.kPolyTypeGround
        NavMeshPolyTypes.kPolyTypeOffMeshConnection

    struct NavMeshWorld
        NavMeshWorld.IsValid()
        NavMeshWorld.GetDefaultWorld()

    struct NavMeshPathQuery
        NavMeshPathQuery.InitSlicedFindPath()
        NavMeshPathQuery.UpdateSlicedFindPath()
        NavMeshPathQuery.FinalizeSlicedFindPath()
        NavMeshPathQuery.GetPathResult()

    struct NavMeshQuery
        NavMeshQuery.MapLocation()
        NavMeshQuery.GetPortalPoints()
        NavMeshQuery.MoveLocations()
        NavMeshQuery.MoveLocation()
        NavMeshQuery.PolygonLocalToWorldMatrix()
        NavMeshQuery.PolygonWorldToLocalMatrix()
        NavMeshQuery.GetPolygonType()
