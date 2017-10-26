using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Collections;

public class TestQuery : MonoBehaviour
{
    public int commandsCount = 1000;
    public int jobThreshold = 16;
    public bool isSingleThreaded = false;
    NativeArray<RaycastHit> results;
    NativeArray<RaycastCommand> commands;

    // Use this for initialization
    void Start()
    {
        results = new NativeArray<RaycastHit>(commandsCount, Allocator.Persistent);
        commands = new NativeArray<RaycastCommand>(commandsCount, Allocator.Persistent);

        for (int n = 0; n < commandsCount; ++n)
            commands[n] = new RaycastCommand(new Vector3(0, 0, -10), Vector3.forward, float.MaxValue, Physics.DefaultRaycastLayers);
    }

    void OnDestroy()
    {
        results.Dispose();
        commands.Dispose();
    }

    void RunSingleThreaded()
    {
        RaycastHit hit = new RaycastHit();

        for (int n = 0; n < commandsCount; ++n)
            Physics.Raycast(new Vector3(0, 0, -10), Vector3.forward, out hit);
    }

    void Update()
    {
        if (isSingleThreaded)
        {
            RunSingleThreaded();
        }
        else
        {
            RaycastCommand.ScheduleBatch(commands, results, jobThreshold).Complete();
        }
    }
}
