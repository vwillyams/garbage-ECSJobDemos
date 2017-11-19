using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Jobs;
using UnityEngine.Experimental.AI;

public class Crowd : MonoBehaviour
{
    public int agentSpawnCount = 16;
    public GameObject prefab;
    public bool drawDebug = false;

    NativeArray<Vector3> m_Positions;
    NativeArray<NavMeshLocation> m_Locations;
    NativeArray<Vector3> m_Velocities;
    NativeArray<PathQueryQueue.Handle> m_RequestHandles;

    // Workaround for missing support for nested arrays
    List<PolygonPath> m_Paths;

    int m_Count;
    TransformAccessArray m_TransformArray;
    JobHandle m_WriteFence;
    PathQueryQueue m_QueryQueue;

    void OnEnable()
    {
        m_Count = agentSpawnCount;
        m_QueryQueue = new PathQueryQueue();
        m_Positions = new NativeArray<Vector3>(m_Count, Allocator.Persistent);
        m_Velocities = new NativeArray<Vector3>(m_Count, Allocator.Persistent);
        m_Locations = new NativeArray<NavMeshLocation>(m_Count, Allocator.Persistent);
        m_RequestHandles = new NativeArray<PathQueryQueue.Handle>(m_Count, Allocator.Persistent);
        m_TransformArray = new TransformAccessArray(m_Count, -1);
        m_Paths = new List<PolygonPath>();

        for (int i = 0; i < m_Count; ++i)
        {
            m_Positions[i] = new Vector3(Random.Range(-10.0f, 10.0f), 0, Random.Range(-10.0f, 10.0f));
            m_Locations[i] = NavMeshQuery.MapLocation(m_Positions[i], 10.0f * Vector3.one, 0, NavMesh.AllAreas);

            var path = new PolygonPath();
            m_Paths.Add(path);
        }

        for (int i = 0; i < m_Count; ++i)
        {
            var go = GameObject.Instantiate(prefab, m_Locations[i].position, Quaternion.identity) as GameObject;
            m_TransformArray.Add(go.transform);
        }
    }

    void OnDisable()
    {
        m_Velocities.Dispose();
        m_Positions.Dispose();
        m_Locations.Dispose();
        m_RequestHandles.Dispose();
        m_TransformArray.Dispose();
        m_QueryQueue.Dispose();
        foreach (var path in m_Paths)
        {
            if (path.polygons.IsCreated)
                path.polygons.Dispose();
        }
    }

    // TODO: make parallel. Currently no support for array of arrays (or List)
    //  this has to run on main thread (no IJobParallelFor)
    public struct AdvancePathJob //: IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<NavMeshLocation> loc;
        public List<PolygonPath> paths;

        public void Execute(int index)
        {
            var path = paths[index];

            int i = 0;
            for (; i < path.size; ++i)
            {
                if (path.polygons[i].polygon == loc[index].polygon)
                    break;
            }
            if (i == 0) return;

            // Shorten the path
            path.size = path.size - i;
            for (int j = 0; j < path.size; ++j)
                path.polygons[j] = path.polygons[i + j];

            paths[index] = path;
        }
    }

    // TODO: make parallel. Currently no support for array of arrays (or List)
    //  this has to run on main thread (no IJobParallelFor)
    public struct UpdateVelocityJob //: IJobParallelFor
    {
        [ReadOnly]
        public List<PolygonPath> paths;
        [ReadOnly]
        public NativeArray<Vector3> pos;
        public NativeArray<Vector3> vel;

        public void Execute(int index)
        {
            var p = paths[index];
            var currentPos = pos[index];
            var endPos = p.end.position;

            var steeringTarget = endPos;

            if (p.size > 1)
            {
                const int maxCorners = 2;
                var straightPath = new NativeArray<NavMeshLocation>(maxCorners, Allocator.TempJob);
                var straightPathFlags = new NativeArray<NavMeshStraightPathFlags>(straightPath.Length, Allocator.TempJob);
                var vertexSide = new NativeArray<float>(straightPath.Length, Allocator.TempJob);
                var cornerCount = 0;
                var pathStatus = PathUtils.FindStraightPath(currentPos, endPos, p.polygons, p.size, ref straightPath, ref straightPathFlags, ref vertexSide, ref cornerCount, straightPath.Length);
                steeringTarget = pathStatus == PathQueryStatus.Success && cornerCount > 1 ? straightPath[1].position : currentPos;

                straightPath.Dispose();
                straightPathFlags.Dispose();
                vertexSide.Dispose();

                //{
                //    Vector3 left, right;
                //    var p0 = p.polygons[0];
                //    var p1 = p.polygons[1];
                //    if (NavMeshQuery.GetPortalPoints(p0, p1, out left, out right))
                //    {
                //        Vector3 cpa1, cpa2;
                //        GeometryUtils.SegmentSegmentCPA(out cpa1, out cpa2, left, right, currentPos, endPos);
                //        steeringTarget = cpa1;
                //    }
                //}
            }
            var velocity = steeringTarget - currentPos;
            velocity.y = 0.0f;
            vel[index] = velocity.normalized;

            // TODO: add avoidance as a job after this one
        }
    }

    public struct MoveLocationsJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Vector3> vel;
        public NativeArray<Vector3> pos;
        public NativeArray<NavMeshLocation> loc;
        NavMeshQuery query;
        public float dt;

        public void Execute(int index)
        {
            // Update the position
            pos[index] = pos[index] + vel[index] * dt;

            // Constrain the position using the location
            var newLoc = NavMeshQuery.MoveLocation(loc[index], pos[index]);
            pos[index] = newLoc.position;
            loc[index] = newLoc;

            // TODO: Patch the path here and remove AdvancePathJob. The path can get shorter, longer, the same.
            // For extending paths - it requires a variant of MoveLocation returning the visited paths.
        }
    }

    public struct WriteTransformJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<Vector3> pos;
        [ReadOnly]
        public NativeArray<Vector3> vel;

        public void Execute(int index, TransformAccess transform)
        {
            transform.position = pos[index];
            if (vel[index].sqrMagnitude > 0.1f)
                transform.rotation = Quaternion.LookRotation(vel[index]);
        }
    }

    void GetPathResults()
    {
        var results = m_QueryQueue.GetAndClearResults();
        foreach (var res in results)
        {
            for (int i = 0; i < m_RequestHandles.Length; ++i)
            {
                if (m_RequestHandles[i].Equals(res.handle))
                {
                    m_RequestHandles[i] = new PathQueryQueue.Handle();
                    if (m_Paths[i].polygons.IsCreated)
                        m_Paths[i].polygons.Dispose();
                    m_Paths[i] = res;
                    break;
                }
            }
        }
    }

    void QueryNewPaths()
    {
        for (int i = 0; i < m_Count; ++i)
        {
            if (m_RequestHandles[i].valid || m_Paths[i].size > 1)
                continue;

            // If there's no path - or close to destination: pick a new destination
            if (m_Paths[i].size == 0 || Vector3.SqrMagnitude(m_Paths[i].end.position - m_Locations[i].position) < 1.0f)
            {
                var dest = new Vector3(Random.Range(-10.0f, 10.0f), 0, Random.Range(-10.0f, 10.0f));
                m_RequestHandles[i] = m_QueryQueue.QueueRequest(m_Locations[i].position, dest, NavMesh.AllAreas);
            }
        }
    }

    void DrawDebug()
    {
        if (!drawDebug)
            return;

        for (int i = 0; i < m_Count; ++i)
        {
            var offset = 0.5f * Vector3.up;
            Debug.DrawRay(m_Positions[i] + offset, m_Velocities[i], Color.yellow);

            if (m_Paths[i].size == 0)
                continue;

            offset = 0.9f * offset;
            Debug.DrawLine(m_Positions[i] + offset, m_Paths[i].end.position + offset, Color.grey);
        }
    }

    void Start()
    {
        m_Count = agentSpawnCount;
    }

    void Update()
    {
        m_WriteFence.Complete();
        DrawDebug();

        m_QueryQueue.Update(100);

        QueryNewPaths();
        GetPathResults();

        var advance = new AdvancePathJob() { loc = m_Locations, paths = m_Paths };
        for (int i = 0; i < m_Count; ++i) advance.Execute(i);

        var vel = new UpdateVelocityJob() { paths = m_Paths, pos = m_Positions, vel = m_Velocities };
        for (int i = 0; i < m_Count; ++i) vel.Execute(i);

        var move = new MoveLocationsJob() { pos = m_Positions, loc = m_Locations, vel = m_Velocities, dt = Time.deltaTime };
        var write = new WriteTransformJob() { pos = m_Positions, vel = m_Velocities };

        var moveFence = move.Schedule(m_Count, 10);
        m_WriteFence = write.Schedule(m_TransformArray, moveFence);
    }
}
