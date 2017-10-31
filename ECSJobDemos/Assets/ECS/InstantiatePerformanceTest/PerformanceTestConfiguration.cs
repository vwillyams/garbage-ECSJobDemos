using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Profiling;
using Unity.Collections;

public static class PerformanceTestConfiguration
{
    public const int InstanceCount = 10 * 1000;
    public static bool CleanManagers = false;
}