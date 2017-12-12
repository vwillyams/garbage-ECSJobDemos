using UnityEngine;
using UnityEngine.ECS;

public class GameSettings : MonoBehaviour
{
    public GameObject playerPrefab;

    public float asteroidRadius;
    public float playerRadius;
    public float bulletRadius;

    public float asteroidVelocity;
    public float playerForce;
    public float bulletVelocity;

    public int mapWidth;
    public int mapHeight;

    public EntityArchetype playerArchetype;
    public EntityArchetype asteroidArchetype;
    public EntityArchetype bulletArchetype;
    public EntityArchetype playerClientArchetype;
    public EntityArchetype asteroidClientArchetype;
    public EntityArchetype bulletClientArchetype;
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
            typeof(NetworkIdCompmonentData),
            typeof(VelocityComponentData));

        asteroidArchetype = m_EntityManager.CreateArchetype(
            typeof(PositionComponentData),
            typeof(RotationComponentData),
            typeof(AsteroidTagComponentData),
            typeof(CollisionSphereComponentData),
            typeof(NetworkIdCompmonentData),
            typeof(VelocityComponentData));

        bulletArchetype = m_EntityManager.CreateArchetype(
            typeof(PositionComponentData),
            typeof(RotationComponentData),
            typeof(BulletTagComponentData),
            typeof(BulletAgeComponentData),
            typeof(CollisionSphereComponentData),
            typeof(NetworkIdCompmonentData),
            typeof(VelocityComponentData));

        playerClientArchetype = m_EntityManager.CreateArchetype(
            typeof(PositionComponentData),
            typeof(RotationComponentData),
            typeof(PlayerTagComponentData),
            typeof(PlayerInputComponentData),
            typeof(NetworkIdCompmonentData),
            typeof(ParticleEmitterComponentData));

        asteroidClientArchetype = m_EntityManager.CreateArchetype(
            typeof(PositionComponentData),
            typeof(RotationComponentData),
            typeof(AsteroidTagComponentData),
            typeof(NetworkIdCompmonentData));

        bulletClientArchetype = m_EntityManager.CreateArchetype(
            typeof(PositionComponentData),
            typeof(RotationComponentData),
            typeof(BulletTagComponentData),
            typeof(NetworkIdCompmonentData));

            asteroidRadius = 15f;
            playerRadius = 10f;
            bulletRadius = 1.0f;

            // TODO: temp hack
            mapWidth = Screen.width;
            mapHeight = Screen.height;
    }
}