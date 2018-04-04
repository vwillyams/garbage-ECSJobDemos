
using Unity.Entities;
using Unity.Entities.Properties;
using UnityEngine;

namespace Unity.Entities.Editor
{
    public class EntitySelectionProxy : ScriptableObject
    {
        public EntityContainer Container { get; private set; }
        public Entity Entity { get; private set; }
        public EntityManager Manager { get; private set; }
        public World World { get; private set; }

        public bool Exists => Manager != null && Manager.IsCreated && Manager.Exists(Entity);

        public void SetEntity(World world, Entity entity)
        {
            this.World = world;
            this.Entity = entity;
            this.Manager = world.GetExistingManager<EntityManager>();
            this.Container = new EntityContainer(Manager, Entity);
        }
    }
}
