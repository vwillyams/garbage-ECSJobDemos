using Unity.ECS;

namespace UnityEngine.ECS
{
    #if !UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP
    class AutomaticWorldBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            DefaultWorldInitialization.Initialize();
        }
    }
    #endif
}