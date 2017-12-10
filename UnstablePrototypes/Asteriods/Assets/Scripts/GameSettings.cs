using UnityEngine;
using UnityEngine.ECS;

public class GameSettings : MonoBehaviour
{
    public GameObject playerPrefab;
    public GameObject bulletPrefab;
    public GameObject asteroidPrefab;

    public float asteroidRadius;
    public float playerRadius;
    public float bulletRadius;

    public EntityArchetype playerArchetype;
    public EntityArchetype asteroidArchetype;
    public EntityArchetype bulletArchetype;
    EntityManager m_EntityManager;

    static GameSettings m_Instance;
    public static GameSettings Instance()
    {
        return m_Instance;
    }

    protected void OnEnable()
    {
        m_Instance = this;

        m_EntityManager = World.Active.GetOrCreateManager<EntityManager>();

        playerArchetype = m_EntityManager.CreateArchetype(
            typeof(PositionComponentData), 
            typeof(RotationComponentData),
            typeof(PlayerTagComponentData),
            typeof(CollisionSphereComponentData),
            typeof(VelocityComponentData));

        asteroidArchetype = m_EntityManager.CreateArchetype(
            typeof(PositionComponentData), 
            typeof(RotationComponentData),
            typeof(AsteroidTagComponentData),
            typeof(CollisionSphereComponentData),
            typeof(VelocityComponentData));

        bulletArchetype = m_EntityManager.CreateArchetype(
            typeof(PositionComponentData), 
            typeof(RotationComponentData),
            typeof(BulletTagComponentData),
            typeof(CollisionSphereComponentData),
            typeof(VelocityComponentData));

            asteroidRadius = 1.5f;
            playerRadius = 1.0f;
            bulletRadius = 0.1f;
    }
}