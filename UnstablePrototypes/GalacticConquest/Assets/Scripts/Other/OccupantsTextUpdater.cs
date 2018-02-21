using Data;
using Unity.ECS;
using UnityEngine;

namespace Other
{
    public class OccupantsTextUpdater : MonoBehaviour
    {
        private Entity _planetEntity;
        private TextMesh _text;
        private int LastOccupantCount = -1;
        [SerializeField] private EntityManager _entityManager;

        private void Start()
        {
            _entityManager = World.Active.GetOrCreateManager<EntityManager>();
            _planetEntity = transform.parent.GetComponent<GameObjectEntity>().Entity;
            _text = GetComponent<TextMesh>();

        }

        private void Update()
        {
            var data = _entityManager.GetComponentData<PlanetData>(_planetEntity);
            if (data.Occupants == LastOccupantCount)
                return;
            LastOccupantCount = data.Occupants;
            _text.text = LastOccupantCount.ToString();
        }
    }
}
