using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using UnityEngine.Experimental.AI;
using Random = UnityEngine.Random;

public class MultiplePaths : MonoBehaviour
{
    [UnityEngine.Range(1, 1000)]
    public int pathfindIterationsPerUpdate = 100;
    public bool drawDebug = true;

    public Transform[] origins;
    public Transform[] targets;

    NativeArray<Vector3> m_Origins;
    NativeArray<Vector3> m_Targets;
    NativeArray<NavMeshLocation> m_OriginLocations;
    NativeArray<NavMeshLocation> m_TargetLocations;
    List<NativeArray<NavMeshLocation>> m_StraightPathCorners;
    List<NativeArray<NavMeshStraightPathFlags>> m_StraightPathCornersFlags;

    // Workaround for missing support for nested arrays
    List<PolygonPathEcs> m_Paths;
    PathQueryQueue m_QueryQueue;
    NativeArray<PathQueryQueue.Handle> m_PathRequestHandles;
    NativeArray<JobHandle> m_CornersJobHandles;

    Vector3 m_MappingExtents = new Vector3(10, 10, 10);

    void OnEnable()
    {
        var originsCount = origins != null ? origins.Length : 0;
        var targetsCount = targets != null ? targets.Length : 0;
        m_Origins = new NativeArray<Vector3>(originsCount, Allocator.Persistent);
        m_Targets = new NativeArray<Vector3>(targetsCount, Allocator.Persistent);
        m_OriginLocations = new NativeArray<NavMeshLocation>(originsCount, Allocator.Persistent);
        m_TargetLocations = new NativeArray<NavMeshLocation>(targetsCount, Allocator.Persistent);
        m_StraightPathCorners = new List<NativeArray<NavMeshLocation>>();
        m_StraightPathCornersFlags = new List<NativeArray<NavMeshStraightPathFlags>>();

        m_Paths = new List<PolygonPathEcs>(targetsCount);
        m_QueryQueue = new PathQueryQueue();
        m_PathRequestHandles = new NativeArray<PathQueryQueue.Handle>(targetsCount, Allocator.Persistent);
        m_CornersJobHandles = new NativeArray<JobHandle>(targetsCount, Allocator.Persistent);
    }

    void OnDisable()
    {
        m_Origins.Dispose();
        m_Targets.Dispose();
        m_OriginLocations.Dispose();
        m_TargetLocations.Dispose();
        m_PathRequestHandles.Dispose();
        m_CornersJobHandles.Dispose();

        foreach (var corners in m_StraightPathCorners)
        {
            corners.Dispose();
        }
        m_StraightPathCorners.Clear();

        foreach (var cornersFlags in m_StraightPathCornersFlags)
        {
            cornersFlags.Dispose();
        }
        m_StraightPathCornersFlags.Clear();

        foreach (var path in m_Paths)
        {
            path.polygons.Dispose();
        }
        m_Paths.Clear();
        m_QueryQueue.Dispose();
    }

    void Update()
    {
        DrawDebug();

        UpdatePositions();

        var mapOrigins = new MapLocationsJob() { pos = m_Origins, loc = m_OriginLocations, extents = m_MappingExtents };
        var mapTargets = new MapLocationsJob() { pos = m_Targets, loc = m_TargetLocations, extents = m_MappingExtents };

        var mapOriginsFence = mapOrigins.Schedule(mapOrigins.pos.Length, 3);
        var mapTargetsFence = mapTargets.Schedule(mapTargets.pos.Length, 2);

        var mappingFence = JobHandle.CombineDependencies(mapOriginsFence, mapTargetsFence);
        mappingFence.Complete();

        var pathsCount = m_Paths.Count;
        for (var i = 0; i < pathsCount; i++)
        {
            if (!m_CornersJobHandles[i].isDone)
                continue;

            while (m_StraightPathCorners.Count <= i)
            {
                m_StraightPathCorners.Add(new NativeArray<NavMeshLocation>(pathsCount, Allocator.Persistent));
            }
            while (m_StraightPathCornersFlags.Count <= i)
            {
                m_StraightPathCornersFlags.Add(new NativeArray<NavMeshStraightPathFlags>(pathsCount, Allocator.Persistent));
            }
            if (m_StraightPathCorners[i].Length < m_Paths[i].size || m_StraightPathCornersFlags[i].Length < m_Paths[i].size)
            {
                m_StraightPathCorners[i].Dispose();
                m_StraightPathCornersFlags[i].Dispose();
                m_StraightPathCorners[i] = new NativeArray<NavMeshLocation>(m_Paths[i].size + 1, Allocator.Persistent);
                m_StraightPathCornersFlags[i] = new NativeArray<NavMeshStraightPathFlags>(m_Paths[i].size + 1, Allocator.Persistent);
            }

            if (m_Paths[i].size > 0)
            {
                var findCorners = new FindWaypointsJob()
                {
                    path = m_Paths[i],
                    startPos = m_Paths[i].start.position + Random.insideUnitSphere,
                    straightPath = m_StraightPathCorners[i],
                    straightPathFlags = m_StraightPathCornersFlags[i],
                    vertexSide = new NativeArray<float>()
                };
                m_CornersJobHandles[i] = findCorners.Schedule(mapTargetsFence);
            }
        }

        var cornersFence = JobHandle.CombineDependencies(m_CornersJobHandles);
        cornersFence.Complete();

        m_QueryQueue.Update(pathfindIterationsPerUpdate);

        QueryNewPaths();

        GetPathResults();
    }

    void UpdatePositions()
    {
        var originsCount = origins != null ? origins.Length : 0;
        var targetsCount = targets != null ? targets.Length : 0;
        var originsCountChanged = originsCount != m_Origins.Length;
        var targetCountChanged = targetsCount != m_Targets.Length;
        if (originsCountChanged)
        {
            m_Origins.Dispose();
            m_Origins = new NativeArray<Vector3>(originsCount, Allocator.Persistent);

            m_OriginLocations.Dispose();
            m_OriginLocations = new NativeArray<NavMeshLocation>(originsCount, Allocator.Persistent);
        }

        if (targetCountChanged)
        {
            m_Targets.Dispose();
            m_Targets = new NativeArray<Vector3>(targetsCount, Allocator.Persistent);

            m_TargetLocations.Dispose();
            m_TargetLocations = new NativeArray<NavMeshLocation>(targetsCount, Allocator.Persistent);

            m_PathRequestHandles.Dispose(); //TODO What happens if some requests were in progress
            m_PathRequestHandles = new NativeArray<PathQueryQueue.Handle>(targetsCount, Allocator.Persistent);

            m_CornersJobHandles.Dispose();
            m_CornersJobHandles = new NativeArray<JobHandle>(targetsCount, Allocator.Persistent);
            for (var i = 0; i < m_CornersJobHandles.Length; i++)
            {
                m_CornersJobHandles[i] = new JobHandle();
                m_CornersJobHandles[i].Complete();
            }
        }

        if (origins != null)
        {
            for (var i = 0; i < originsCount; i++)
            {
                var origin = origins[i];
                m_Origins[i] = origin != null ? origin.position : Vector3.zero;
            }
        }
        if (targets != null)
        {
            for (var i = 0; i < targetsCount; i++)
            {
                var target = targets[i];
                m_Targets[i] = target != null ? target.position : Vector3.zero;
            }
        }

        var diff = m_Paths.Count - m_Targets.Length;
        if (diff > 0)
        {
            for (var i = m_Targets.Length; i < m_Paths.Count; i++)
            {
                m_Paths[i].polygons.Dispose();
            }
            m_Paths.RemoveRange(m_Targets.Length, diff);
        }
        for (var i = diff; i < 0; i++)
        {
            var path = new PolygonPathEcs();
            m_Paths.Add(path);
        }
    }

    void QueryNewPaths()
    {
        Assert.IsTrue(m_TargetLocations.Length == m_Paths.Count && m_TargetLocations.Length == m_PathRequestHandles.Length);

        for (var i = 0; i < m_TargetLocations.Length; ++i)
        {
            if (m_PathRequestHandles[i].valid /*|| !m_TargetLocations[i].valid*/ /* || m_Paths[i].size > 1*/)
                continue;

            // TODO Query for new paths only when the start/end polygonID changes ?

            var oIdx = Math.Min(i, m_OriginLocations.Length - 1);
            if (m_Paths[i].size == 0 || !m_OriginLocations[oIdx].Equals(m_Paths[i].start) || !m_TargetLocations[i].Equals(m_Paths[i].end))
            {
                if (m_OriginLocations[oIdx].valid && m_TargetLocations[i].valid)
                {
                    var src = m_OriginLocations[oIdx].position;
                    var dest = m_TargetLocations[i].position;
                    m_PathRequestHandles[i] = m_QueryQueue.QueueRequest(src, dest, NavMesh.AllAreas);
                }
            }
        }
    }

    void GetPathResults()
    {
        var results = m_QueryQueue.GetAndClearResults();
        foreach (var res in results)
        {
            for (var i = 0; i < m_PathRequestHandles.Length; ++i)
            {
                if (m_PathRequestHandles[i].Equals(res.handle))
                {
                    m_PathRequestHandles[i] = new PathQueryQueue.Handle();
                    if (m_Paths[i].polygons.IsCreated)
                        m_Paths[i].polygons.Dispose();
                    var resEcs = new PolygonPathEcs { start = res.start, end = res.end, polygons = res.polygons, size = res.size };
                    m_Paths[i] = resEcs;
                    break;
                }
            }
        }
    }

    void DrawDebug()
    {
        if (!drawDebug)
            return;

        var offset = 0.05f * Vector3.up;
        var k = 0;
        foreach (var corners in m_StraightPathCorners)
        {
            for (var i = 0; i < corners.Length - 1; ++i)
            {
                var loc1 = corners[i];
                var loc2 = corners[i + 1];
                if (loc1.polygon != 0 && loc2.polygon != 0)
                {
                    var color = m_StraightPathCornersFlags[k][i] == NavMeshStraightPathFlags.kStraightPathOffMeshConnection ? Color.yellow : Color.green;
                    Debug.DrawLine(loc1.position + offset, loc2.position + offset, color);
                }
            }

            ++k;
        }
    }

    public struct MapLocationsJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Vector3> pos;
        public NativeArray<NavMeshLocation> loc;
        public Vector3 extents;

        public void Execute(int index)
        {
            loc[index] = NavMeshQuery.MapLocation(pos[index], extents, 0);
        }
    }

    public struct FindWaypointsJob : IJob
    {
        static bool s_UseFunneling = true;

        [ReadOnly]
        public PolygonPathEcs path;
        [ReadOnly]
        public Vector3 startPos;

        public NativeArray<NavMeshLocation> straightPath;
        public NativeArray<NavMeshStraightPathFlags> straightPathFlags;
        public NativeArray<float> vertexSide;
        public int maxStraightPath;

        public void Execute()
        {
            if (path.size > 1)
            {
                if (s_UseFunneling)
                {
                    var cornerCount = 0;
                    var maxCount = maxStraightPath > 1 ? maxStraightPath : straightPath.Length;
                    PathUtils.FindStraightPath(path, ref straightPath, ref straightPathFlags, ref vertexSide, ref cornerCount, maxCount);
                    if (cornerCount < straightPath.Length)
                    {
                        straightPath[cornerCount] = new NavMeshLocation(); //empty terminator
                    }
                }
                else
                {
                    Vector3 left, right;
                    var p0 = path.polygons[0];
                    var p1 = path.polygons[1];
                    if (NavMeshQuery.GetPortalPoints(p0, p1, out left, out right))
                    {
                        float3 cpa1, cpa2;
                        GeometryUtils.SegmentSegmentCPA(out cpa1, out cpa2, left, right, startPos, path.end.position);
                        straightPath[0] = NavMeshQuery.MapLocation(cpa1, Vector3.one, 0);
                        const int cornerCount = 1;
                        if (cornerCount < straightPath.Length)
                        {
                            straightPath[cornerCount] = new NavMeshLocation(); //empty terminator
                        }
                    }
                }
            }
        }
    }
}
