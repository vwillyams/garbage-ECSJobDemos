using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Profiling;
using Unity.Collections;

public static class PerformanceTestConfiguration
{
    public const int InstanceCount = 10000;
    public const int Iterations = 500;
    public static bool CleanManagers = false;
}