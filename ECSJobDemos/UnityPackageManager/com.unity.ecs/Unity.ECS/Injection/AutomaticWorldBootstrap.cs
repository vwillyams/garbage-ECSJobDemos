using UnityEngine;

namespace Unity.ECS
{
#if !UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP
    internal static class AutomaticWorldBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            DefaultWorldInitialization.Initialize();
        }
    }
#endif
}
