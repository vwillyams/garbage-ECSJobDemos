using System.Collections.Generic;
using System.Linq;
using Data;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

public class PlanetSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _planetPrefab;
    [SerializeField] private int _initialCount = 10;
    [SerializeField] readonly float radius = 10.0f;
    private EntityManager _entityManager;
    [SerializeField] private Material[] _teamMaterials;
    static Dictionary<Entity, GameObject> entities = new Dictionary<Entity, GameObject>();

    public static Material[] TeamMaterials;

    private void Instantiate(int count)
    {
        var planetOwnership = new List<int>
        {
            1, 1,
            2, 2
        };

        for (var i = 0; i < count; ++i)
        {


            var sphereRadius = 2.0f;
            var safe = false;
            float3 pos;
            int attempts = 0;
            do
            {
                if (++attempts >= 100)
                {
                    Debug.LogAssertion("Tried spawning planets too many times. Aborting");
                }
                var randomValue = (Vector3) Random.insideUnitSphere;
                randomValue.y = 0;
                pos = (randomValue * radius) + new Vector3(transform.position.x, transform.position.z);
                var collisions = Physics.OverlapSphere(pos, sphereRadius);
                if (!collisions.Any())
                    safe = true;
            } while (!safe);
            var randomRotation = Random.insideUnitSphere;
            var go = GameObject.Instantiate(_planetPrefab, pos, Quaternion.identity);
            var planetEntity = go.GetComponent<GameObjectEntity>().Entity;
            var meshGo = go.GetComponentsInChildren<Transform>().First(c => c.gameObject != go).gameObject;
            var meshEntity = meshGo.GetComponent<GameObjectEntity>().Entity;

            var randomScale = Random.Range(1.0f, 4.0f);
            meshGo.transform.localScale = new Vector3(randomScale, randomScale, randomScale);

            var planetData = new PlanetData
            {
                TeamOwnership = 0,
                Radius = randomScale * 0.5f,
                Position = pos
            };
            var rotationData = new RotationData
            {
                RotationSpeed = randomRotation
            };
            if (planetOwnership.Any())
            {
                planetData.TeamOwnership = planetOwnership.First();
                planetOwnership.Remove(planetData.TeamOwnership);
            }
            else
            {
                planetData.Occupants = Random.Range(1, 100);
            }
            entities[planetEntity] = go;
            SetColor(planetEntity, planetData.TeamOwnership);

            _entityManager.AddComponent(planetEntity, planetData);
            _entityManager.AddComponent(meshEntity, rotationData);
        }
    }

    private void OnEnable()
    {
        TeamMaterials = _teamMaterials;
        _entityManager = World.Active.GetOrCreateManager<EntityManager>();
        Instantiate(_initialCount);
    }

    public static void SetColor(Entity entity, int team)
    {
        var go = entities[entity];
        go.GetComponentsInChildren<MeshRenderer>().First(c => c.gameObject != go).material = TeamMaterials[team];
    }
}
