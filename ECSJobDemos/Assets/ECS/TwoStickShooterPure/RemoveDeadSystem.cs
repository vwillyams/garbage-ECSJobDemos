using Unity.Collections;
using UnityEngine.ECS;

namespace TwoStickPureExample
{
    /// <summary>
    /// This system deletes entities that have a Health component with a value less than or equal to zero.
    /// </summary>
    public class RemoveDeadSystem : ComponentSystem
    {
        private struct Data
        {
            public int Length;
            [ReadOnly] public EntityArray Entity;
            [ReadOnly] public ComponentDataArray<Health> Health;
        }

        [Inject] private Data m_Data;

        private struct PlayerCheck
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<PlayerInput> PlayerInput;
        }

        [Inject] private PlayerCheck m_PlayerCheck;

        protected override void OnUpdate()
        {
            int removeCount = 0;
            var enemiesToRemove = new NativeArray<Entity>(m_Data.Length, Allocator.Temp);

            bool playerDead = m_PlayerCheck.Length == 0;

            for (int i = 0; i < m_Data.Length; ++i)
            {
                if (m_Data.Health[i].Value <= 0.0f || playerDead)
                {
                    enemiesToRemove[removeCount++] = m_Data.Entity[i];
                }
            }

            // TODO: Why is this sometimes throwing?
            // EntityManager.DestroyEntity(enemiesToRemove.Slice(0, removeCount));
            //
            // ArgumentException: All entities passed to EntityManager.Destroy must exist. One of the entities was already destroyed or never created.
            // UnityEngine.ECS.EntityDataManager.AssertEntitiesExist (UnityEngine.ECS.Entity* entities, System.Int32 count) (at UnityPackageManager/com.unity.ecs/Unity.ECS/EntityDataManager.cs:117)
            // UnityEngine.ECS.EntityManager.DestroyEntityInternal (UnityEngine.ECS.Entity* entities, System.Int32 count) (at UnityPackageManager/com.unity.ecs/Unity.ECS/EntityManager.cs:229)
            // UnityEngine.ECS.EntityManager.DestroyEntity (Unity.Collections.NativeSlice`1[T] entities) (at UnityPackageManager/com.unity.ecs/Unity.ECS/EntityManager.cs:217)
            // TwoStickExample.DestroyDeadSystem.OnUpdate () (at Assets/ECS/TwoStickShooterPure/EnemySystem.cs:232)
            // UnityEngine.ECS.ComponentSystem.InternalUpdate () (at UnityPackageManager/com.unity.ecs/Unity.ECS/ComponentSystem.cs:174)
            // UnityEngine.ECS.ScriptBehaviourManager.Update () (at UnityPackageManager/com.unity.ecs/Unity.ECS/ScriptBehaviourManager.cs:50)
            // UnityEngine.ECS.ScriptBehaviourUpdateOrder+DummyDelagateWrapper.TriggerUpdate () (at UnityPackageManager/com.unity.ecs/Unity.ECS/ScriptBehaviourUpdateOrder.cs:57)

            for (int i = 0; i < removeCount; ++i)
            {
                EntityManager.DestroyEntity(enemiesToRemove[i]);
            }

            enemiesToRemove.Dispose();
        }
    }

}
