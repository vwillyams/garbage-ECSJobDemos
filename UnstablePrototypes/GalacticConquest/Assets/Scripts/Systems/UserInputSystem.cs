using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;
using UnityEngine.ECS;

namespace Systems
{
    public class UserInputSystem : ComponentSystem
    {
        private Dictionary<GameObject, PlanetData?> FromTargets = new Dictionary<GameObject, PlanetData?>();
        private GameObject ToTarget = null;

        private EntityManager _entityManager;

        public UserInputSystem ()
        {
            _entityManager = World.Active.GetOrCreateManager<EntityManager>();
        }

        protected override void OnUpdate () {
            if (Input.GetMouseButtonDown(0))
            {
                var planet = GetPlanetUnderMouse();
                if (planet == null)
                {
                    FromTargets.Clear();
                    Debug.Log("Clicked outside, so we cleared the from selection");
                }
                else
                {
                    if (FromTargets.ContainsKey(planet))
                    {
                        Debug.Log("Deselecting from target " + planet.name);
                        FromTargets.Remove(planet);
                    }
                    else
                    {
                        var entity = planet.GetComponent<GameObjectEntity>().Entity;
                        var data = _entityManager.GetComponent<PlanetData>(entity);
                        var previousTarget = FromTargets.Values.FirstOrDefault();
                        if ((previousTarget == null || previousTarget.Value.TeamOwnership == data.TeamOwnership) && data.TeamOwnership != 0)
                        {
                            Debug.Log("Selecting from target " + planet.name);
                            FromTargets[planet] = data;
                        }
                        else
                        {
                            if (data.TeamOwnership == 0)
                            {
                                Debug.LogWarning("You cant set a netural planet as a from planet");
                            }
                            else
                            {
                                Debug.Log("Adding planet to from target, but clearing the previous list since it is of a different team");
                                FromTargets.Clear();
                                FromTargets[planet] = data;
                            }
                        }

                    }
                }

            }
            if (Input.GetMouseButtonDown(1))
            {
                var planet = GetPlanetUnderMouse();
                if (planet == null)
                {
                    Debug.Log("Deselecting to target ");
                    ToTarget = null;
                }
                else
                {
                    if (!FromTargets.Any())
                    {
                        Debug.Log("No planets selected to send from, skipping");
                        return;
                    }
                    Debug.Log("Setting To target to " + planet.name);
                    ToTarget = planet;
                    foreach (var p in FromTargets.Keys)
                    {
                        if (p == ToTarget)
                            continue;
                        var entity = p.GetComponent<GameObjectEntity>().Entity;
                        var meshComponent = p.GetComponentsInChildren<GameObjectEntity>().First(c => c.gameObject != p.gameObject);
                        var occupantData = _entityManager.GetComponent<PlanetData>(entity);
                        var targetEntity = ToTarget.GetComponent<GameObjectEntity>().Entity;
                        var launchData = new PlanetShipLaunchData
                        {
                            TargetEntity = targetEntity,
                            TeamOwnership = occupantData.TeamOwnership,
                            NumberToSpawn = occupantData.Occupants,
                            SpawnLocation = p.transform.position,
                            SpawnRadius = meshComponent.transform.lossyScale.x * 0.5f
                        };

                        occupantData.Occupants = 0;
                        _entityManager.SetComponent(entity, occupantData);
                        if (_entityManager.HasComponent<PlanetShipLaunchData>(entity))
                        {
                            _entityManager.SetComponent(entity, launchData);
                            continue;
                        }
                        _entityManager.AddComponent(entity, launchData);
                    }
                }
            }
        }

        private GameObject GetPlanetUnderMouse()
        {
            RaycastHit hit;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, 1 << LayerMask.NameToLayer("Planet")))
            {
                return hit.collider.transform.gameObject;
            }
            return null;
        }
    }
}
