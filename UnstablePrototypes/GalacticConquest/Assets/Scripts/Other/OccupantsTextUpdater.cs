using Data;
using Unity.Entities;
using UnityEngine;

namespace Other
{
    public class OccupantsTextUpdater : MonoBehaviour
    {
        Entity _planetEntity;
        TextMesh _text;
        int LastOccupantCount = -1;
        [SerializeField]
        EntityManager _entityManager;

        void Start()
        {
            _entityManager = World.Active.GetOrCreateManager<EntityManager>();
            _planetEntity = transform.parent.GetComponent<GameObjectEntity>().Entity;
            _text = GetComponent<TextMesh>();

        }

        void Update()
        {
            var data = _entityManager.GetComponentData<PlanetData>(_planetEntity);
            if (data.Occupants == LastOccupantCount)
                return;
            LastOccupantCount = data.Occupants;
            _text.text = LastOccupantCount.ToString();
        }
    }
}
