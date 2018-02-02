using System.Collections.Concurrent;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Rendering;
using UnityEngine.ECS.Transform;
using UnityEngine.Experimental.LowLevel;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;

namespace TwoStickClassicExample
{
    public sealed class TwoStickBootstrap
    {
        public static TwoStickExampleSettings Settings;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InitializeWithScene()
        {
            var settingsGO = GameObject.Find("Settings");
            Settings = settingsGO?.GetComponent<TwoStickExampleSettings>();
            if (!Settings)
                return;

            var player = Object.Instantiate(Settings.PlayerPrefab);
            player.Position = new float2(0, 0);
            player.Heading = new float2(0, 1);
        }
    }
}
