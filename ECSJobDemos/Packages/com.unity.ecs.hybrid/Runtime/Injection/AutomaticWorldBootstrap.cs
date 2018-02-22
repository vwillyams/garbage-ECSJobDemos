using UnityEngine;

namespace Unity.ECS.Hybrid
{
#if !UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP
    static class AutomaticWorldBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            DefaultWorldInitialization.Initialize();
        }
    }
#endif
}
