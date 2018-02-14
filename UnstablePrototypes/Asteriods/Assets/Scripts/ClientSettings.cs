using UnityEngine;
using Unity.ECS;

using Unity.Multiplayer;

public class ClientSettings
{
    public GameObject playerPrefab;

    public EntityArchetype playerClientArchetype;
    public EntityArchetype shipArchetype;
    public EntityArchetype playerBaseShipArchetype;
    public EntityArchetype asteroidClientArchetype;
    public EntityArchetype bulletClientArchetype;
    public NetworkClient networkClient;
    public World world;
    public string serverAddress;
    public ushort serverPort;

    static ClientSettings m_Instance;
    public static ClientSettings Instance()
    {
        return m_Instance;
    }

    public static void Create(World world)
    {
        m_Instance = new ClientSettings();
        m_Instance.Instantiate(world);
    }

    void Instantiate(World world)
    {
        this.networkClient = new NetworkClient();
        this.world = world;
        playerPrefab = Resources.Load("Prefabs/Ship") as GameObject;
        var manager = world.GetOrCreateManager<EntityManager>();

        playerBaseShipArchetype = manager.CreateArchetype(
            typeof(ShipTagComponentData),
            typeof(ShipStateComponentData),
            typeof(NetworkIdCompmonentData));

        shipArchetype = manager.CreateArchetype(
            typeof(ShipTagComponentData),
            typeof(ShipStateComponentData),
            typeof(PositionComponentData),
            typeof(RotationComponentData),
            typeof(ParticleEmitterComponentData),
            typeof(NetworkIdCompmonentData));


        playerClientArchetype = manager.CreateArchetype(
            typeof(PlayerTagComponentData),
            typeof(ShipInfoComponentData),
            typeof(PlayerInputComponentData),
            typeof(NetworkIdCompmonentData),
            typeof(PlayerStateComponentData));

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

        serverAddress = "127.0.0.1";
        serverPort = 50001;

        var configuration = new SocketConfiguration()
        {
            SendBufferSize = ushort.MaxValue,
            RecvBufferSize = ushort.MaxValue,
            Timeout = uint.MaxValue,
            MaximumConnections = 1
        };
    }
}
