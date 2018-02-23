using Unity.Collections;
using Unity.Entities;
using UnityEngine.ECS.SimpleMovement;
using Unity.Transforms;

namespace TwoStickPureExample
{
    public class ShotSpawnSystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            public EntityArray SpawnedEntities;
            [ReadOnly] public ComponentDataArray<ShotSpawnData> SpawnData;
        }

        [Inject] private Data m_Data;

        protected override void OnUpdate()
        {
            var em = EntityManager;

            // Need to copy the data out so we can spawn without invalidating these arrays.
            // TODO: This cannot use an entity command buffer atm because it doesn't have shared component data support.
            var entities = new NativeArray<Entity>(m_Data.Length, Allocator.Temp);
            var spawnData = new NativeArray<ShotSpawnData>(m_Data.Length, Allocator.Temp);
            m_Data.SpawnedEntities.CopyTo(entities);
            m_Data.SpawnData.CopyTo(spawnData);

            for (int i = 0; i < m_Data.Length; ++i)
            {
                var sd = spawnData[i];
                var shotEntity = entities[i];
                em.RemoveComponent<ShotSpawnData>(shotEntity);
                em.AddSharedComponentData(shotEntity, default(MoveForward));
                em.AddComponentData(shotEntity, sd.Shot);
                em.AddComponentData(shotEntity, sd.Position);
                em.AddComponentData(shotEntity, sd.Heading);
                em.AddComponentData(shotEntity, sd.Faction);
                em.AddComponentData(shotEntity, default(TransformMatrix));
                if (sd.Faction.Value == Faction.kPlayer)
                {
                    em.AddComponentData(shotEntity, new MoveSpeed {speed = TwoStickBootstrap.Settings.bulletMoveSpeed});
                }
                else
                {
                    em.AddComponentData(shotEntity, new MoveSpeed {speed = TwoStickBootstrap.Settings.enemyShotSpeed});
                }
                em.AddSharedComponentData(shotEntity, sd.Faction.Value == Faction.kPlayer ? TwoStickBootstrap.PlayerShotLook : TwoStickBootstrap.EnemyShotLook);
            }

            spawnData.Dispose();
            entities.Dispose();
        }
    }
}
