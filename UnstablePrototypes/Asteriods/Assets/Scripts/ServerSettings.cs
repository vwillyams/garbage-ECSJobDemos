using UnityEngine;
using UnityEngine.ECS;

using Unity.Multiplayer;

public class ServerSettings
{
    public float asteroidRadius;
    public float playerRadius;
    public float bulletRadius;

    public float asteroidVelocity;
    public float playerForce;
    public float bulletVelocity;

    public EntityArchetype playerArchetype;
    public EntityArchetype asteroidArchetype;
    public EntityArchetype bulletArchetype;

    public NetworkServer networkServer;
    public World world;

    static ServerSettings m_Instance;
    public static ServerSettings Instance()
    {
        return m_Instance;
    }

    public static void Create(World world)
    {
        m_Instance = new ServerSettings();
        m_Instance.Instantiate(world);
    }

    void Instantiate(World world)
    {
        this.networkServer = new NetworkServer("127.0.0.1", 50001);
        this.world = world;
        var manager = world.GetOrCreateManager<EntityManager>();

        asteroidRadius = 15f;
        playerRadius = 10f;
        bulletRadius = 1.0f;
        asteroidVelocity = 0.15f;
        playerForce = 1.0f;
        bulletVelocity = 10f;

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

        var configuration = new SocketConfiguration()
        {
            SendBufferSize = ushort.MaxValue,
            RecvBufferSize = ushort.MaxValue,
            Timeout = uint.MaxValue,
            MaximumConnections = 10
        };
    }
}
