using System;
using System.Collections;
using System.Collections.Generic;
using Unity.ECS;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.LowLevel;

namespace UnityEditor.ECS
{
    [InitializeOnLoad]
    public class PlayerLoopHelper
    {

        public static event Action<PlayerLoopSystem> OnUpdatePlayerLoop;

        public static PlayerLoopSystem currentPlayerLoop { get; private set; }
        
        static PlayerLoopHelper()
        {
            World.OnSetPlayerLoop += UpdatePlayerLoop;
        }
	
        static void UpdatePlayerLoop(PlayerLoopSystem newPlayerLoop)
        {
            currentPlayerLoop = newPlayerLoop;
		    OnUpdatePlayerLoop?.Invoke(newPlayerLoop);
        }
    }

}