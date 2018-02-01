using UnityEngine;

namespace TwoStickHybridExample
{
    public class TwoStickExampleSettings : MonoBehaviour
    {
        public float playerMoveSpeed = 15.0f;
        public float bulletMoveSpeed = 30.0f;
        public float bulletTimeToLive = 2.0f;
        public float playerFireCoolDown = 0.1f;
        public float enemySpeed = 8.0f;

        public Shot ShotPrefab;
        public Transform2D PlayerPrefab;
        public Enemy EnemyPrefab;
        public EnemySpawnSystemState EnemySpawnState;
    }
}