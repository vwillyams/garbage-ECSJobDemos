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
	    public enum Phase
	    {
	        FixedUpdatePrePhysic,
	        FixedUpdatePostPhysic,
	        PreUpdate,
	        Update,
	        PreLateUpdate,
	        PostLateUpdate,
	        COUNT // Need to be hidden
	    }

	    private static List<CallbackFunction>[] s_DelegateMethods = new List<CallbackFunction>[(int)PlayerLoopManager.Phase.COUNT];
	    private static List<CallbackFunction> s_DomainUnloadMethods = new List<CallbackFunction>();

	    static PlayerLoopManager()
	    {
	        var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
	        //LogLoop(playerLoop);
	        var customPlayerLoop = new PlayerLoopSystem();
	        customPlayerLoop.name = "ECSPlayerLoop";
	        customPlayerLoop.subSystemList = new PlayerLoopSystem[playerLoop.subSystemList.Length + 4];
	        int outPos = 0;
	        for (int i = 0; i < playerLoop.subSystemList.Length; ++i, ++outPos)
	        {
	            if (playerLoop.subSystemList[i].name == "FixedUpdate")
	            {
	                PlayerLoopSystem customFixedUpdate = playerLoop.subSystemList[i];
	                customFixedUpdate.subSystemList = new PlayerLoopSystem[customFixedUpdate.subSystemList.Length + 2];
	                int fixOutPos = 0;
	                for (int fixi = 0; fixi < playerLoop.subSystemList[i].subSystemList.Length; ++fixi, ++fixOutPos)
	                {
	                    if (playerLoop.subSystemList[i].subSystemList[fixi].name == "FixedUpdate.LegacyFixedAnimationUpdate")
	                    {
	                        customFixedUpdate.subSystemList[fixOutPos].name = "ECS.FixedUpdatePrePhysic";
	                        customFixedUpdate.subSystemList[fixOutPos].updateDelegate = InvokeFixedUpdatePrePhysicPlayerLoop;
	                        ++fixOutPos;
	                    }
	                    else if (playerLoop.subSystemList[i].subSystemList[fixi].name == "FixedUpdate.ScriptRunDelayedTasks")
	                    {
	                        customFixedUpdate.subSystemList[fixOutPos].name = "ECS.FixedUpdatePostPhysic";
	                        customFixedUpdate.subSystemList[fixOutPos].updateDelegate = InvokeFixedUpdatePostPhysicPlayerLoop;
	                        ++fixOutPos;
	                    }
	                    customFixedUpdate.subSystemList[fixOutPos] = playerLoop.subSystemList[i].subSystemList[fixi];
	                }
	                customPlayerLoop.subSystemList[outPos] = customFixedUpdate;
	                continue;
	            }
	            else if (playerLoop.subSystemList[i].name == "Update.ScriptRunBehaviourUpdate")
	            {
	                customPlayerLoop.subSystemList[outPos].name = "ECS.PreUpdate";
	                customPlayerLoop.subSystemList[outPos].updateDelegate = InvokePreUpdatePlayerLoop;
	                ++outPos;
	            }
	            else if (playerLoop.subSystemList[i].name == "PreLateUpdate.AIUpdatePostScript")
	            {
	                customPlayerLoop.subSystemList[outPos].name = "ECS.Update";
	                customPlayerLoop.subSystemList[outPos].updateDelegate = InvokeUpdatePlayerLoop;
	                ++outPos;
	            }
	            else if (playerLoop.subSystemList[i].name == "PostLateUpdate.PlayerSendFrameStarted")
	            {
	                customPlayerLoop.subSystemList[outPos].name = "ECS.PreLateUpdate";
	                customPlayerLoop.subSystemList[outPos].updateDelegate = InvokePreLateUpdatePlayerLoop;
	                ++outPos;
	            }
	            customPlayerLoop.subSystemList[outPos] = playerLoop.subSystemList[i];
	        }
	        customPlayerLoop.subSystemList[outPos].name = "ECS.PostLateUpdate";
	        customPlayerLoop.subSystemList[outPos].updateDelegate = InvokePostLateUpdatePlayerLoop;
	        ++outPos;
	        PlayerLoop.SetPlayerLoop(customPlayerLoop);
	        // TODO: this does not guarantee the order or destruction
	        var go = new GameObject();
	        go.AddComponent<PlayerLoopDisableManager>().isActive = true;
	        go.hideFlags = HideFlags.HideAndDontSave;
	    }

	    public delegate void CallbackFunction();

	    public static void RegisterDomainUnload(CallbackFunction callback)
	    {
	        s_DomainUnloadMethods.Add(callback);
	    }

	    public static void RegisterUpdate(CallbackFunction callback, PlayerLoopManager.Phase phase)
	    {
	        if (s_DelegateMethods[(int)phase] == null)
	            s_DelegateMethods[(int)phase] = new List<CallbackFunction>();

	        s_DelegateMethods[(int)phase].Add(callback);
	    }

	    public static void UnregisterUpdate(CallbackFunction callback)
	    {
	        foreach (var delegates in s_DelegateMethods)
	            if (delegates != null)
	                delegates.Remove(callback);
	    }

	    private static void InvokeFixedUpdatePrePhysicPlayerLoop()
	    {
	        InvokePlayerLoopByPhase(PlayerLoopManager.Phase.FixedUpdatePrePhysic);
	    }

	    private static void InvokeFixedUpdatePostPhysicPlayerLoop()
	    {
	        InvokePlayerLoopByPhase(PlayerLoopManager.Phase.FixedUpdatePostPhysic);
	    }

	    private static void InvokePreUpdatePlayerLoop()
	    {
	        InvokePlayerLoopByPhase(PlayerLoopManager.Phase.PreUpdate);
	    }

	    private static void InvokeUpdatePlayerLoop()
	    {
	        InvokePlayerLoopByPhase(PlayerLoopManager.Phase.Update);
	    }

	    private static void InvokePreLateUpdatePlayerLoop()
	    {
	        InvokePlayerLoopByPhase(PlayerLoopManager.Phase.PreLateUpdate);
	    }

	    private static void InvokePostLateUpdatePlayerLoop()
	    {
	        InvokePlayerLoopByPhase(PlayerLoopManager.Phase.PostLateUpdate);
	    }

	    internal static void InvokeBeforeDomainUnload()
	    {
	        if (s_DomainUnloadMethods != null)
	        {
	            InvokeMethods(s_DomainUnloadMethods);
	        }
	    }

	    private static void InvokePlayerLoopByPhase(Phase phase)
	    {
	        if (s_DelegateMethods[(int)phase] != null)
	            InvokeMethods(s_DelegateMethods[(int)phase]);
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