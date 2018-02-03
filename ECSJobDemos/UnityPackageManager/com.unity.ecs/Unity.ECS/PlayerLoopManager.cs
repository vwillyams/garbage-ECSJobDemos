using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.ECS
{
    public class PlayerLoopManager
    {
        private struct UnloadMethod : IComparable<UnloadMethod>
        {
            public CallbackFunction Function;
            public int Ordering;

            public int CompareTo(UnloadMethod other)
            {
                return Ordering - other.Ordering;
            }
        };

        private static List<UnloadMethod> s_DomainUnloadMethods = new List<UnloadMethod>();

        static PlayerLoopManager()
        {
            var go = new GameObject();
            go.AddComponent<PlayerLoopDisableManager>().isActive = true;
            go.hideFlags = HideFlags.HideInHierarchy;
            if (Application.isPlaying)
                GameObject.DontDestroyOnLoad(go);
            else
                go.hideFlags = HideFlags.HideAndDontSave;
        }

        public delegate void CallbackFunction();

        /// <summary>
        /// Register a function to be called when the scripting domain is unloading.
        /// </summary>
        /// <param name="callback">The function to call</param>
        /// <param name="ordering">The ordering. Lower ordering values get called earlier.</param>
        public static void RegisterDomainUnload(CallbackFunction callback, int ordering = 0)
        {
            s_DomainUnloadMethods.Add(new UnloadMethod { Function = callback, Ordering = ordering });
        }

        internal static void InvokeBeforeDomainUnload()
        {
            if (s_DomainUnloadMethods != null)
            {
                InvokeMethods(s_DomainUnloadMethods);
            }
        }

        private static void InvokeMethods(List<UnloadMethod> callbacks)
        {
            callbacks.Sort();

            foreach (var m in callbacks)
            {
                var callback = m.Function;

#if !UNITY_WINRT
                UnityEngine.Profiling.Profiler.BeginSample(callback.Method.DeclaringType.Name + "." + callback.Method.Name);
#endif

                // Isolate systems from each other
                try
                {
                    callback();
                }
                catch (System.Exception exc)
                {
                    Debug.LogException(exc);
                }


#if !UNITY_WINRT
                UnityEngine.Profiling.Profiler.EndSample();
#endif
            }
        }
    }
}
