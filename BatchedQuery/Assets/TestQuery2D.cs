using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Collections;

public class TestQuery2D : MonoBehaviour
{
    public int commandsCount = 1000;
    public int jobThreshold = 16;
    public bool isSingleThreaded = false;
    NativeArray<RaycastHit2D> results;
    NativeArray<RaycastCommand2D> rayCommands;
    NativeArray<CircleCastCommand2D> circleCommands;

    public enum QueryType
    {
        Raycast,
        CircleCast
    };

    public QueryType m_QueryType;


    // Use this for initialization
    void Start()
    {
        results = new NativeArray<RaycastHit2D>(commandsCount * 16, Allocator.Persistent);
        CreateBatch();
    }

    void OnDestroy()
    {
        results.Dispose();
        if (rayCommands.IsCreated)
            rayCommands.Dispose();
        if (circleCommands.IsCreated)
            circleCommands.Dispose();
    }

    void RunSingleThreaded()
    {
        if (m_QueryType == QueryType.Raycast)
        {
            for (int n = 0; n < commandsCount; ++n)
                Physics2D.Raycast(new Vector2(0, -10), Vector2.up, 100.0f);
        }
        else if (m_QueryType == QueryType.CircleCast)
        {
            for (int n = 0; n < commandsCount; ++n)
                Physics2D.CircleCast(new Vector2(0, -10), 1.0f, Vector2.up, 100.0f);
        }
    }

    void Update()
    {
        if (isSingleThreaded)
        {
            RunSingleThreaded();
        }
        else
        {
            if (m_QueryType == QueryType.Raycast)
                RaycastCommand2D.ScheduleBatch(rayCommands, results, jobThreshold).Complete();
            else if (m_QueryType == QueryType.CircleCast)
                CircleCastCommand2D.ScheduleBatch(circleCommands, results, jobThreshold).Complete();
        }
    }

    void CreateBatch()
    {
        if (rayCommands.IsCreated)
            rayCommands.Dispose();
        if (circleCommands.IsCreated)
            circleCommands.Dispose();

        rayCommands = new NativeArray<RaycastCommand2D>(commandsCount, Allocator.Persistent);
        for (int n = 0; n < commandsCount; ++n)
            rayCommands[n] = new RaycastCommand2D(new Vector2(0, -10), Vector2.up, 100.0f);
        circleCommands = new NativeArray<CircleCastCommand2D>(commandsCount, Allocator.Persistent);
        for (int n = 0; n < commandsCount; ++n)
            circleCommands[n] = new CircleCastCommand2D(new Vector2(0, -10), 1.0f, Vector2.up, 100.0f);
    }
}
