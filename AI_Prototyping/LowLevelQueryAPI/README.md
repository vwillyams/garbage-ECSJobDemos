This project is meant to work with code from the native branch scripting/jobsystem/navmesh .
Revision needed: **28ed0c7fe78f** (2017-07-28 17:04:48 +0200)

Example scenes:

**CrowdMoveTest**

- showcases how to find paths between many source and destination points in parallel and time-sliced, and have entities move along simplified paths that go through the obtained polygons.

- _TheCrowd_ has _Crowd_ behaviour where the number of entities can be specified.


**PathCorners**

- showcases how to find the corners on a straight path between Origin and Target game objects.

- _PathManager_ has the behaviour MultiplePaths where the Origins and Targets can be changed any time.

	- _Pathfind Iterations Per Update_ specifies the maximum number of NavMesh nodes that are traversed every frame. It serves as a way to delay the end result of pathfinding so that FindStraightPath() would have to wait one or several frames for the path to be ready.

- Origins and Targets don't have to contain the same number of elements

- the Origins and Targets game objects can be moved around at runtime in order to trigger changes in the path between them.


Exclusive API featured:

    namespace UnityEngine.Experimental.AI
    struct PolygonID
        PolygonID.valid

    struct NavMeshLocation
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
