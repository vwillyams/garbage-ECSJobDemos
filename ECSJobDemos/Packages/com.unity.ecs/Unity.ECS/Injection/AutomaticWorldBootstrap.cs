using RuntimeInitializeLoadType = UnityEngine.RuntimeInitializeLoadType;
using RuntimeInitializeOnLoadMethod = UnityEngine.RuntimeInitializeOnLoadMethodAttribute;

namespace Unity.ECS
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
