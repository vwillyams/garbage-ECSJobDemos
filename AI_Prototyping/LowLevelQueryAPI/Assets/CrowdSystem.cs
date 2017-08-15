using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using UnityEngine.Experimental.AI;
using UnityEngine.ECS;
using Random = UnityEngine.Random;

public struct AgentPaths
{
    public struct Path
    {
        public int begin;
        public int size;
        public NavMeshLocation start;
        public NavMeshLocation end;
    }

    public struct JobData
    {
        [ReadOnly]
        public NativeArray<PolygonID> nodes;
        public NativeArray<Path> ranges;

        public NativeSlice<PolygonID> GetPath(int index)
        {
            return new NativeSlice<PolygonID>(nodes, ranges[index].begin, ranges[index].size);
        }

        public Path GetPathInfo(int index)
        {
            return ranges[index];
        }

        public void DiscardFirstNodes(int index, int howMany)
        {
            if (howMany == 0)
                return;

            var pathInfo = ranges[index];
            var end = pathInfo.begin + ranges[index].size;        
            pathInfo.begin = Math.Min(ranges[index].begin + howMany, end);
            pathInfo.size = end - pathInfo.begin;
            Debug.Assert(pathInfo.size >= 0, "This should not happen");
            ranges[index] = pathInfo;
        }
    }

    NativeList<PolygonID> m_PathNodes;
    NativeList<Path> m_PathRanges;
    int m_MaxPathSize;
    public int maxPathSize { get { return m_MaxPathSize; } }
    public int Count { get { return m_PathRanges.Length; } }

    public AgentPaths(int capacity, int maxSize = 64)
    {
        m_MaxPathSize = maxSize;
        m_PathNodes = new NativeList<PolygonID>(capacity * m_MaxPathSize, Allocator.Persistent);
        m_PathRanges = new NativeList<Path>(capacity, Allocator.Persistent);
    }

    public void Dispose()
    {
        m_PathNodes.Dispose();
        m_PathRanges.Dispose();
    }

    public void InitializePath(int index, int nodeCount)
    {
        m_PathRanges[index] = new Path() { begin = m_MaxPathSize * index, size = nodeCount };
    }

    public void AddAgent()
    {
        m_PathRanges.Add(new Path());
        m_PathNodes.ResizeUninitialized(m_MaxPathSize * m_PathRanges.Length);
        InitializePath(m_PathRanges.Length - 1, 0);
    }

    public NativeSlice<PolygonID> GetMaxPath(int index)
    {
        return new NativeSlice<PolygonID>(m_PathNodes, m_MaxPathSize * index, m_MaxPathSize);
    }

    public Path GetPathInfo(int index)
    {
        return m_PathRanges[index];
    }

    public JobData GetJobData()
    {
        var jobData = new JobData() { nodes = m_PathNodes, ranges = m_PathRanges };
        return jobData;
    }

    public void SetPath(int index, NativeSlice<PolygonID> newPath, NavMeshLocation start, NavMeshLocation end)
    {
        var nodeCount = Math.Min(newPath.Length, m_MaxPathSize);
        m_PathRanges[index] = new Path
        {
            begin = m_MaxPathSize * index,
            size = nodeCount,
            start = start,
            end = end
        };
        var agentPath = GetMaxPath(index);
        for (var k = 0; k < nodeCount; k++)
        {
            var node = newPath[k];
            agentPath[k] = node;
        }
    }
}


public class CrowdSystem : JobComponentSystem
{
    public bool drawDebug = false;

    [InjectTuples]
    ComponentDataArray<CrowdAgent> m_Agents;

    const int k_MaxQueryNodes = 2000;
    const int k_QueryCount = 7;

    PathQueryQueueEcs[] m_QueryQueues;
    NativeArray<JobHandle> m_QueryFences;
    NativeArray<UpdateQueriesJob> m_QueryJobs;

    AgentPaths m_AgentPaths;

    override protected void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);

        m_AgentPaths = new AgentPaths(capacity, 128);
        m_QueryQueues = new PathQueryQueueEcs[k_QueryCount];
        m_QueryJobs = new NativeArray<UpdateQueriesJob>(k_QueryCount, Allocator.Persistent);
        m_QueryFences = new NativeArray<JobHandle>(k_QueryCount, Allocator.Persistent);
        for (var i = 0; i < m_QueryQueues.Length; i++)
        {
            m_QueryQueues[i] = new PathQueryQueueEcs(k_MaxQueryNodes);
            m_QueryFences[i] = new JobHandle();
        }
    }

    override protected void OnDestroyManager()
    {
        base.OnDestroyManager();

        for (var i = 0; i < m_QueryQueues.Length; i++)
        {
            m_QueryQueues[i].Dispose();
        }
        m_QueryFences.Dispose();
        m_QueryJobs.Dispose();
        m_AgentPaths.Dispose();
    }

    public struct AdvancePathJob : IJobParallelFor
    {
        [ReadOnly]
        public ComponentDataArray<CrowdAgent> agents;

        public AgentPaths.JobData paths;

        public void Execute(int index)
        {
            var path = paths.GetPath(index);
            var pathInfo = paths.GetPathInfo(index);

            var i = 0;
            for (; i < pathInfo.size; ++i)
            {
                if (path[i].polygon == agents[index].location.polygon)
                    break;
            }
            if (i == 0)
                return;

            // Shorten the path
            paths.DiscardFirstNodes(index, i);
        }
    }

    public struct UpdateVelocityJob : IJobParallelFor
    {
        [ReadOnly]
        public AgentPaths.JobData paths;

        public ComponentDataArray<CrowdAgent> agents;

        public void Execute(int index)
        {
            var agent = agents[index];
            if (!agent.location.valid)
                return;

            var pathInfo = paths.GetPathInfo(index);
            if (pathInfo.size > 0)
            {
                float3 currentPos = agent.location.position;
                float3 endPos = pathInfo.end.position;
                var steeringTarget = endPos;

                if (pathInfo.size > 1)
                {
                    const int maxCorners = 2;
                    var cornerCount = 0;
                    var straightPath = new NativeArray<NavMeshLocation>(maxCorners, Allocator.TempJob);
                    var straightPathFlags = new NativeArray<NavMeshStraightPathFlags>(straightPath.Length, Allocator.TempJob);
                    var pathStatus = PathUtils.FindStraightPath(currentPos, endPos, paths.GetPath(index), pathInfo.size, ref straightPath, ref straightPathFlags, ref cornerCount, straightPath.Length);
                    if (pathStatus == PathQueryStatus.Success && cornerCount > 1)
                    {
                        steeringTarget = straightPath[1].position;
                    }

                    straightPath.Dispose();
                    straightPathFlags.Dispose();
                }

                var velocity = steeringTarget - currentPos;
                velocity.y = 0.0f;
                agent.velocity = math.any(velocity) ? math.normalize(velocity) : new float3(0);
            }
            else
            {
                agent.velocity = new float3(0);
            }

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
            var wantedPos = agent.worldPosition + agent.velocity * dt;

            if (agent.location.valid)
            {
                agent.location = NavMeshQuery.MoveLocation(agent.location, wantedPos);
            }
            else
            {
                // Constrain the position using the location
                // TODO There are two positions that could be mapped: current agent position or the wanted position [#adriant]
                agent.location = NavMeshQuery.MapLocation(wantedPos, 3 * Vector3.one, 0);
            }
            agent.worldPosition = agent.location.position;

            agents[index] = agent;

            // TODO: Patch the path here and remove AdvancePathJob. The path can get shorter, longer, the same.
            //       For extending paths - it requires a variant of MoveLocation returning the visited paths.
        }
    }

    public struct UpdateQueriesJob : IJob
    {
        public PathQueryQueueEcs queryQueue;
        public int maxIterations;

        public void Execute()
        {
            queryQueue.UpdateTimeliced(maxIterations);
        }    
    }

    // TODO QueryNewPaths() should move into a separate job [#adriant]
    void QueryNewPaths()
    {
        for (var i = 0; i < m_Agents.Length; ++i)
        {
            var agent = m_Agents[i];
            if (!agent.location.valid)
                continue;

            // If there's no path - or close to destination: pick a new destination
            var pathInfo = m_AgentPaths.GetPathInfo(i);
            if (pathInfo.size == 0 || math.distance(pathInfo.end.position, agent.location.position) < 1.0f)
            {
                var hasRequest = m_QueryQueues.Any(t => t.HasRequestForAgent(i));
                if (!hasRequest)
                {
                    var minQ = 0;
                    var minRequests = int.MaxValue;
                    for (var q = 0; q < m_QueryQueues.Length; q++)
                    {
                        var reqCount = m_QueryQueues[q].GetRequestCount();
                        if (reqCount < minRequests)
                        {
                            minQ = q;
                            minRequests = reqCount;
                        }
                    }
                    var dest = new Vector3(Random.Range(-10.0f, 10.0f), 0, Random.Range(-10.0f, 10.0f));
                    m_QueryQueues[minQ].QueueRequest(i, agent.location.position, dest, NavMesh.AllAreas);
                }
            }
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
            Debug.DrawRay(agent.worldPosition + offset, agent.velocity, Color.yellow);

            var pathInfo = m_AgentPaths.GetPathInfo(i);
            if (pathInfo.size == 0)
            {
                Debug.DrawRay(agent.worldPosition + offset, 0.5f * Vector3.up, Color.red);
                continue;
            }

            offset = 0.9f * offset;
            float3 pathEndPos = pathInfo.end.position;
            Debug.DrawLine(agent.worldPosition + offset, pathEndPos + offset, Color.black);
        }
    }

    override protected void OnUpdate()
    {
        base.OnUpdate();

        CompleteDependency();

        var missingPaths = m_Agents.Length - m_AgentPaths.Count;
        for (var i = 0; i < missingPaths; ++i)
        {
            m_AgentPaths.AddAgent();
        }

        DrawDebug();

        QueryNewPaths();
        //var requestNewPaths = new RequestNewPathsJob() { agents = m_Agents, requestList = m_RequestList };
        //var requestsFence = requestNewPaths.Schedule(m_Agents.Length, 20);

        for (var i = 0; i < m_QueryJobs.Length; ++i)
        {
            if (m_QueryQueues[i].IsEmpty())
                continue;

            var someVariety = 10 + i * 5;
            m_QueryJobs[i] = new UpdateQueriesJob() { maxIterations = someVariety, queryQueue = m_QueryQueues[i] };
            m_QueryFences[i] = m_QueryJobs[i].Schedule();
        }
        var queriesFence = JobHandle.CombineDependencies(m_QueryFences);

        queriesFence.Complete();

        // TODO CopyResultsTo() should move into a separate job [#adriant]
        foreach (var queryQueue in m_QueryQueues)
        {
            if (queryQueue.GetResultPathsCount() > 0)
            {
                queryQueue.CopyResultsTo(ref m_AgentPaths);
                queryQueue.ClearResults();
            }
        }

        var advance = new AdvancePathJob() { agents = m_Agents, paths = m_AgentPaths.GetJobData() };
        var advanceFence = advance.Schedule(m_Agents.Length, 20);

        var vel = new UpdateVelocityJob() { agents = m_Agents, paths = m_AgentPaths.GetJobData() };
        var velFence = vel.Schedule(m_Agents.Length, 15, advanceFence);

        var move = new MoveLocationsJob() { agents = m_Agents, dt = Time.deltaTime };
        var moveFence = move.Schedule(m_Agents.Length, 10, velFence);

        AddDependency(moveFence);

        // TODO: job safety for navmesh mutation
        // NavMeshManager.DidScheduleQueryJobs(moveFence);
    }
}
