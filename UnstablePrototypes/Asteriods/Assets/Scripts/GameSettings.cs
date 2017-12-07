using UnityEngine;
using UnityEngine.ECS;

public class GameSettings : MonoBehaviour
{
    public GameObject playerPrefab;
    public GameObject bulletPrefab;
    public GameObject asteroidPrefab;

    public EntityArchetype playerArchetype;

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
            typeof(SteeringComponentData));
    }
}