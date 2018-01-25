using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Profiling;
using Unity.Collections;

public static class PerformanceTestConfiguration
{
    public const int InstanceCount = 100 * 1000;
    public const int Iterations = 1;
    public static bool CleanManagers = false;
}
