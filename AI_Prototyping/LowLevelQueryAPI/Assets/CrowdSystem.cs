using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using UnityEngine.Experimental.AI;
using UnityEngine.ECS;

public class CrowdSystem : ComponentSystem
{
    public bool drawDebug = false;

    [InjectTuples]
    ComponentDataArray<CrowdAgent> m_Agents;

    // Workaround for missing support for nested arrays
    List<PolygonPath> m_Paths;

    PathQueryQueue m_QueryQueue;

    override protected void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);

        m_QueryQueue = new PathQueryQueue();

        m_Paths = new List<PolygonPath>(capacity);
    }

    override protected void OnDestroyManager()
    {
        base.OnDestroyManager();

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
        public ComponentDataArray<CrowdAgent> agents;
        public List<PolygonPath> paths;

        public void Execute(int index)
        {
            var path = paths[index];

            int i = 0;
            for (; i < path.size; ++i)
            {
                if (path.polygons[i].polygon == agents[index].location.polygon)
                    break;
            }
            if (i == 0)
                return;

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

        public ComponentDataArray<CrowdAgent> agents;

        public void Execute(int index)
        {
            var agent = agents[index];

            var p = paths[index];
            var currentPos = agent.position;
            float3 endPos = p.end.position;

            var steeringTarget = endPos;

            if (p.size > 1)
            {
                const int maxCorners = 2;
                var straightPath = new NativeArray<NavMeshLocation>(maxCorners, Allocator.TempJob);
                var straightPathFlags = new NativeArray<NavMeshStraightPathFlags>(straightPath.Length, Allocator.TempJob);
                var cornerCount = 0;
                var pathStatus = PathUtils.FindStraightPath(currentPos, endPos, p.polygons, p.size, ref straightPath, ref straightPathFlags, ref cornerCount, straightPath.Length);
                if (pathStatus == PathQueryStatus.Success && cornerCount > 1)
                {
                    steeringTarget = straightPath[1].position;
                }
                else
                {
                    steeringTarget = currentPos;
                }

                straightPath.Dispose();
                straightPathFlags.Dispose();
            }
            var velocity = steeringTarget - currentPos;
            velocity.y = 0.0f;
            agent.velocity = math.normalize(velocity);
            // TODO: add avoidance as a job after this one

            agents[index] = agent;
        }
    }

    public struct MoveLocationsJob : IJobParallelFor
    {
        public ComponentDataArray<CrowdAgent> agents;
        public float dt;

        public void Execute(int index)
        {
            var agent = agents[index];
            // Update the position
            agent.position = agent.position + agent.velocity * dt;

            // Constrain the position using the location
			agent.location = NavMeshQuery.MoveLocation(agent.location, agent.position);
			agent.position = agent.location;

            agents[index] = agent;

            // TODO: Patch the path here and remove AdvancePathJob. The path can get shorter, longer, the same.
            // For extending paths - it requires a variant of MoveLocation returning the visited paths.
        }
    }

    void GetPathResults()
    {
        var results = m_QueryQueue.GetAndClearResults();
        foreach (var res in results)
        {
            bool wasCopied = false;
            for (var i = 0; i < m_Agents.Length; ++i)
            {
                var agent = m_Agents[i];
                if (agent.requestHandle.Equals(res.handle))
                {
                    agent.requestHandle = new PathQueryQueue.Handle();
                    m_Agents[i] = agent;
                    if (m_Paths[i].polygons.IsCreated)
                        m_Paths[i].polygons.Dispose();
                    m_Paths[i] = res;
                    wasCopied = true;
                    break;
                }
            }

            if (!wasCopied)
            {
                res.polygons.Dispose();
            }
        }
    }

    void QueryNewPaths()
    {
        for (var i = 0; i < m_Agents.Length; ++i)
        {
            var agent = m_Agents[i];

            if (!agent.location.valid)
            {
                agent.location = NavMeshQuery.MapLocation(agent.position, 3 * Vector3.one, 0);
            }

            if (!(agent.requestHandle.valid || m_Paths[i].size > 1))
            {
                // If there's no path - or close to destination: pick a new destination
                if (m_Paths[i].size == 0 || math.distance(m_Paths[i].end.position, agent.location.position) < 1.0f)
                {
                    var dest = new Vector3(Random.Range(-10.0f, 10.0f), 0, Random.Range(-10.0f, 10.0f));
                    agent.requestHandle = m_QueryQueue.QueueRequest(agent.location.position, dest, NavMesh.AllAreas);
                }
            }

            m_Agents[i] = agent;
        }
    }

    void DrawDebug()
    {
        if (!drawDebug)
            return;

        for (var i = 0; i < m_Agents.Length; ++i)
        {
            var agent = m_Agents[i];
            float3 offset = 0.5f * Vector3.up;
            Debug.DrawRay(agent.position + offset, agent.velocity, Color.yellow);

            if (m_Paths[i].size == 0)
                continue;

            offset = 0.9f * offset;
            float3 pathEndPos = m_Paths[i].end.position;
            Debug.DrawLine(agent.position + offset, pathEndPos + offset, Color.grey);
        }
    }

    override protected void OnUpdate()
    {
        base.OnUpdate();

        if (m_Paths.Count < m_Agents.Length)
        {
            for (var i = 0; i < m_Agents.Length - m_Paths.Count; ++i)
            {
                var path = new PolygonPath();
                m_Paths.Add(path);
            }
        }

        DrawDebug();

        m_QueryQueue.Update(100);

        QueryNewPaths();
        GetPathResults();

        var advance = new AdvancePathJob() { agents = m_Agents, paths = m_Paths };
        for (int i = 0; i < m_Agents.Length; ++i)
            advance.Execute(i);

        var vel = new UpdateVelocityJob() { agents = m_Agents, paths = m_Paths };
        for (int i = 0; i < m_Agents.Length; ++i)
            vel.Execute(i);

        var move = new MoveLocationsJob() { agents = m_Agents, dt = Time.deltaTime };
        move.Schedule(m_Agents.Length, 10).Complete();
    }
}
