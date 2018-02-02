using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickClassicExample
{

    public class Enemy : MonoBehaviour
    {
        private void Update()
        {
            // Movement
            var settings = TwoStickBootstrap.Settings;
            var speed = settings.enemySpeed;
            var minY = settings.playfield.yMin;
            var maxY = settings.playfield.yMax;

            var xform = GetComponent<Transform2D>();
            xform.Position.y -= speed;

            if (xform.Position.y > maxY || xform.Position.y < minY)
            {
                GetComponent<Health>().Value = -1;
            }
            
            // Shooting

            var player = FindObjectOfType<Player>();
            if (!player)
                return;
            var playerPos = player.GetComponent<Transform2D>().Position;

            var state = GetComponent<EnemyShootState>();

            state.Cooldown -= Time.deltaTime;
            
            if (state.Cooldown <= 0.0)
            {
                state.Cooldown = TwoStickBootstrap.Settings.enemyShootRate;
                var position = GetComponent<Transform2D>().Position;

                ShotSpawnData spawn = new ShotSpawnData()
                {
                    Position = position,
                    Heading = math.normalize(playerPos - position),
                    Faction = TwoStickBootstrap.Settings.EnemyFaction
                };
                ShotSpawnSystem.SpawnShot(spawn);
            }
        }
    }
}
