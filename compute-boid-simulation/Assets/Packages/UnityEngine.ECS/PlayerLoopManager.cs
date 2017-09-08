using System;
using System.Runtime.InteropServices;
using UnityEngine.Scripting;
using System.Collections.Generic;
using UnityEngine.Bindings;
using UnityEngine;

namespace UnityEngine.ECS
{
	public class PlayerLoopManager
	{
	    private static List<CallbackFunction> s_DomainUnloadMethods = new List<CallbackFunction>();

	    static PlayerLoopManager()
	    {
	        // TODO: this does not guarantee the order or destruction
	        var go = new GameObject();
	        go.AddComponent<PlayerLoopDisableManager>().isActive = true;
            go.hideFlags = HideFlags.HideInHierarchy;
            if (Application.isPlaying)
                GameObject.DontDestroyOnLoad(go);
            else
                go.hideFlags = HideFlags.HideAndDontSave;
	    }

	    public delegate void CallbackFunction();

	    public static void RegisterDomainUnload(CallbackFunction callback)
	    {
	        s_DomainUnloadMethods.Add(callback);
	    }

	    internal static void InvokeBeforeDomainUnload()
	    {
	        if (s_DomainUnloadMethods != null)
	        {
	            InvokeMethods(s_DomainUnloadMethods);
	        }
	    }
			
	    private static void InvokeMethods(List<CallbackFunction> callbacks)
	    {
	        foreach (var callback in callbacks)
	        {
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