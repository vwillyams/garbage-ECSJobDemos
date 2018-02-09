using Unity.Mathematics;
using UnityEngine;

namespace TwoStickHybridExample
{
    public sealed class TwoStickBootstrap
    {
        public static TwoStickExampleSettings Settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
        }


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
            
            
            var oldState = Random.state;
            Random.InitState(0xaf77);
            
            var state = Settings.EnemySpawnState;
            state.Cooldown = 0.0f;
            state.SpawnedEnemyCount = 0;
            state.RandomState = Random.state;
            Random.state = oldState;
        }
    }
}
