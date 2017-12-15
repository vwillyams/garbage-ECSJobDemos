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

    static GameSettings m_Instance;
    public static GameSettings Instance()
    {
        return m_Instance;
    }

    protected void OnEnable()
    {
        m_Instance = this;

        // HACK:
        var manager = LocalWorldBootstrap.serverWorld.GetOrCreateManager<EntityManager>();

        playerArchetype = manager.CreateArchetype(
            typeof(PositionComponentData),
            typeof(RotationComponentData),
            typeof(PlayerTagComponentData),
            typeof(CollisionSphereComponentData),
            typeof(NetworkIdCompmonentData),
            typeof(VelocityComponentData));

        asteroidArchetype = manager.CreateArchetype(
            typeof(PositionComponentData),
            typeof(RotationComponentData),
            typeof(AsteroidTagComponentData),
            typeof(CollisionSphereComponentData),
            typeof(NetworkIdCompmonentData),
            typeof(VelocityComponentData));

        bulletArchetype = manager.CreateArchetype(
            typeof(PositionComponentData),
            typeof(RotationComponentData),
            typeof(BulletTagComponentData),
            typeof(BulletAgeComponentData),
            typeof(CollisionSphereComponentData),
            typeof(NetworkIdCompmonentData),
            typeof(VelocityComponentData));

        manager = LocalWorldBootstrap.clientWorld.GetOrCreateManager<EntityManager>();

        playerClientArchetype = manager.CreateArchetype(
            typeof(PositionComponentData),
            typeof(RotationComponentData),
            typeof(PlayerTagComponentData),
            typeof(PlayerInputComponentData),
            typeof(NetworkIdCompmonentData),
            typeof(ParticleEmitterComponentData));

        asteroidClientArchetype = manager.CreateArchetype(
            typeof(PositionComponentData),
            typeof(RotationComponentData),
            typeof(AsteroidTagComponentData),
            typeof(NetworkIdCompmonentData));

        bulletClientArchetype = manager.CreateArchetype(
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
