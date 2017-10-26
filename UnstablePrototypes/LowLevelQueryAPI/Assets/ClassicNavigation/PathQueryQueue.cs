using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.AI;
using UnityEngine.Jobs;
using UnityEngine.Collections;


public struct PolygonPath
{
    public PathQueryQueue.Handle handle;
    public NativeArray<PolygonID> polygons;
    public NavMeshLocation start;
    public NavMeshLocation end;
    public int size;
}

public class PathQueryQueue
{
    public struct Handle
    {
        public int id;
        public bool valid { get { return id != 0; } }
    };

    NavMeshPathQuery m_Query;
    Queue<Request> m_Requests;
    List<PolygonPath> m_Results;
    NativeArray<float> m_Costs;

    PolygonPath m_Current;
    int m_HandleID;

    struct Request
    {
        public Handle handle;
        public int mask;
        public Vector3 start;
        public Vector3 end;
    }

    public PathQueryQueue()
    {
        var world = NavMeshWorld.GetDefaultWorld();
        m_Query = new NavMeshPathQuery(world, 2000, Allocator.Persistent);
        m_Requests = new Queue<Request>();
        m_Results = new List<PolygonPath>();
        m_Costs = new NativeArray<float>(32, Allocator.Persistent);
        for (int i = 0; i < m_Costs.Length; ++i)
            m_Costs[i] = 1.0f;
    }

    public void Dispose()
    {
        m_Costs.Dispose();

        foreach (var path in m_Results)
        {
            path.polygons.Dispose();
        }
    }

    Handle GetNewHandle()
    {
        // TODO: check existing requests for collisions
        while (++m_HandleID == 0) {}
        return new Handle { id = m_HandleID };
    }

    public Handle QueueRequest(Vector3 start, Vector3 end, int areaMask)
    {
        var req = new Request { start = start, end = end, mask = areaMask, handle = GetNewHandle() };
        m_Requests.Enqueue(req);
        return req.handle;
    }

    public List<PolygonPath> GetAndClearResults()
    {
        var res = m_Results;
        m_Results = new List<PolygonPath>();
        return res;
    }

    public bool TryGetPolygonPath(ref PolygonPath path, Handle handle)
    {
        for (int i = 0; i < m_Results.Count; ++i)
        {
            if (m_Results[i].handle.Equals(handle))
            {
                path.polygons = m_Results[i].polygons;
                path.start = m_Results[i].start;
                path.end = m_Results[i].end;
                path.size = m_Results[i].size;
                m_Results.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public void Update(int maxIter = 100)
    {
        while (maxIter > 0)
        {
            if (!m_Current.handle.valid)
            {
                if (m_Requests.Count == 0)
                    return;

                // Initialize a new request
                var req = m_Requests.Dequeue();
                m_Current.handle = req.handle;
                m_Current.start = NavMeshQuery.MapLocation(req.start, 10.0f * Vector3.one, 0, -1);
                m_Current.end = NavMeshQuery.MapLocation(req.end, 10.0f * Vector3.one, 0, -1);
                // TODO: check the status returned by InitSlicedFindPath()
                m_Query.InitSlicedFindPath(m_Current.start, m_Current.end, 0, m_Costs, -1);
            }

            if (m_Current.handle.valid)
            {
                // Continue existing request
                int niter = 0;
                // TODO: check status
                var status = m_Query.UpdateSlicedFindPath(maxIter, out niter);
                maxIter -= niter;

                if (status == PathQueryStatus.Success)
                {
                    // Copy path result to
                    int npath = 0;
                    status = m_Query.FinalizeSlicedFindPath(out npath);

                    var res = m_Current;
                    res.polygons = new NativeArray<PolygonID>(npath, Allocator.Persistent);
                    res.size = m_Query.GetPathResult(res.polygons);

                    m_Results.Add(res);
                    m_Current = new PolygonPath();
                }
            }
        }
    }
}
