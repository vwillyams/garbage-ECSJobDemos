using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

// Nice way of specifing rules
// Nested heirarcies
// Offline baking of scene
// Inspector showing result of conversion
// Baking of static colliders

// Streaming game objects and pure scene together
// Assosiating game objects and pure entities based on scene

// Parent applying rules which is not entity and not part of hierarchy

public interface EntityConvertionRule
{
    System.Type[] ConvertedComponents();
    bool Convert(EntityManager entityManager, Entity ent, GameObject go);
}
static public class EntityConvertionExtensions
{
    public static void EnsurePosition(this EntityConvertionRule rule, EntityManager entityManager, Entity ent, GameObject go)
    {
        if (!entityManager.HasComponent(ent, typeof(Position)) && !entityManager.HasComponent(ent, typeof(LocalPosition)))
        {
            entityManager.AddComponentData(ent, new Position(go.transform.position));
        }
    }
    public static void EnsureRotation(this EntityConvertionRule rule, EntityManager entityManager, Entity ent, GameObject go)
    {
        if (!entityManager.HasComponent(ent, typeof(Rotation)) && !entityManager.HasComponent(ent, typeof(LocalRotation)))
        {
            entityManager.AddComponentData(ent, new Rotation(go.transform.rotation));
        }
    }
    public static void EnsureTransformMatrix(this EntityConvertionRule rule, EntityManager entityManager, Entity ent, GameObject go)
    {
        if (!entityManager.HasComponent(ent, typeof(TransformMatrix)))
        {
            entityManager.AddComponentData(ent, new TransformMatrix {Value = go.transform.localToWorldMatrix});
        }
    }
}

class MeshEntityConversionRule : EntityConvertionRule
{
    public System.Type[] ConvertedComponents()
    {
        return new System.Type[]{typeof(MeshRenderer), typeof(MeshFilter)};
    }

    public bool Convert(EntityManager entityManager, Entity ent, GameObject go)
    {
        MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
        MeshFilter mesh = go.GetComponent<MeshFilter>();
        if (!go.isStatic)
        {
            if (go.transform.position != Vector3.zero)
                this.EnsurePosition(entityManager, ent, go);
            if (go.transform.rotation != Quaternion.identity)
                this.EnsureRotation(entityManager, ent, go);
        }

        var rend = new MeshInstanceRenderer();
        rend.mesh = mesh.sharedMesh;

        rend.castShadows = meshRenderer.shadowCastingMode;
        rend.receiveShadows = meshRenderer.receiveShadows;

        for (int i = 0; i < meshRenderer.sharedMaterials.Length; ++i)
        {
            rend.material = meshRenderer.sharedMaterials[i];
            rend.subMesh = i;
            rend.material.enableInstancing = true;
            if (!rend.material.enableInstancing)
            {
                Debug.LogWarning("Object contains a Material which does not support instancing and cannot be converted to ECS", go);
                continue;
            }
            var meshEnt = entityManager.CreateEntity();
            if (go.isStatic)
                entityManager.AddComponentData(meshEnt, new TransformMatrix {Value = go.transform.localToWorldMatrix});
            else
            {
                entityManager.AddComponentData(meshEnt, new TransformMatrix());
                entityManager.AddComponentData(meshEnt, new TransformParent(ent));
                entityManager.AddComponentData(meshEnt, new LocalPosition());
                entityManager.AddComponentData(meshEnt, new LocalRotation(quaternion.identity));
            }

            entityManager.AddSharedComponentData(meshEnt, rend);
        }

        return true;
    }
}

#if HAVE_BOIDS
class BoidEntityConversionRule : EntityConvertionRule
{
    public System.Type[] ConvertedComponents()
    {
        return new System.Type[]{typeof(BoidMonoComponent)};
    }

    public bool Convert(EntityManager entityManager, Entity ent, GameObject go)
    {
        this.EnsurePosition(entityManager, ent, go);
        //this.EnsureRotation(entityManager, ent, go);
        //this.EnsureTransformMatrix(entityManager, ent, go);

        var boid = go.GetComponent<BoidMonoComponent>().BoidData;
        entityManager.AddSharedComponentData(ent, boid);

        entityManager.AddComponentData(ent, new Heading(go.transform.forward));
        entityManager.AddComponentData(ent, new MoveSpeed {speed = go.GetComponent<BoidMonoComponent>().Speed});
        entityManager.AddSharedComponentData(ent, new MoveForward());
        return true;
    }
}
    #endif

public class PureScene : MonoBehaviour {

	// Use this for initialization
    private List<EntityConvertionRule> conversionRules;
	void Start ()
	{
	    conversionRules = new List<EntityConvertionRule>();
	    conversionRules.Add(new MeshEntityConversionRule());
#if HAVE_BOIDS
	    conversionRules.Add(new BoidEntityConversionRule());
#endif

	    var entityManager = World.Active.GetOrCreateManager<EntityManager>();

        ConvertChildren(entityManager, gameObject, Entity.Null);
        gameObject.SetActive(false);
	}

    void ConvertChildren(EntityManager entityManager, GameObject go, Entity transformParent)
    {
        for (int i = 0; i < go.transform.childCount; ++i)
        {
            var nextTransParent = Entity.Null;
            var entGo = go.transform.GetChild(i).gameObject;
            if (!entGo.activeSelf)
                continue;
            var ent = entityManager.CreateEntity();

            if (transformParent != Entity.Null && !entGo.isStatic)
            {
                entityManager.AddComponentData(ent, new TransformParent(transformParent));
                entityManager.AddComponentData(ent, new LocalPosition(entGo.transform.localPosition));
                entityManager.AddComponentData(ent, new LocalRotation(entGo.transform.localRotation));
                nextTransParent = ent;
            }
            var allComponents = new HashSet<Component>(entGo.GetComponents<Component>());
            // No one is allowed to convert the transform directly since it is already added
            allComponents.Remove(entGo.transform);
            bool wasConverted = false;
            foreach (var converter in conversionRules)
            {
                bool validRule = true;
                for (int comp = 0; comp < converter.ConvertedComponents().Length; ++comp)
                {
                    var component = entGo.GetComponent(converter.ConvertedComponents()[comp]);
                    validRule &= component != null && allComponents.Contains(component);
                }

                if (validRule)
                {
                    if (converter.Convert(entityManager, ent, entGo))
                    {
                        for (int comp = 0; comp < converter.ConvertedComponents().Length; ++comp)
                        {
                            var component = entGo.GetComponent(converter.ConvertedComponents()[comp]);
                            allComponents.Remove(component);
                        }
                        wasConverted = true;
                    }
                }
            }

            if (!wasConverted)
            {
                // Entity was now converted and is either not a heiarachy parent or can be skipped in the hierarchy
                if (transformParent == Entity.Null || entGo.isStatic ||
                    (entGo.transform.localPosition == Vector3.zero) &&
                    entGo.transform.localRotation == Quaternion.identity)
                {
                    entityManager.DestroyEntity(ent);
                    nextTransParent = transformParent;
                }
            }

            // This entity contains something and is the root of a transform, so make sure it has a proper transform setup
            if (wasConverted && transformParent == Entity.Null && !entGo.isStatic)
            {
                if (entGo.transform.position != Vector3.zero)
                {
                    if (!entityManager.HasComponent<Position>(ent))
                        entityManager.AddComponentData(ent, new Position(entGo.transform.position));
                }
                if (entGo.transform.rotation != Quaternion.identity)
                {
                    if (!entityManager.HasComponent<Rotation>(ent))
                        entityManager.AddComponentData(ent, new Rotation(entGo.transform.rotation));
                }
                // FIXME: scale

                // If there is any component which can modify the transform of this entity it needs to be a transform parent for its children
                if (entityManager.HasComponent<Position>(ent) || entityManager.HasComponent<Rotation>(ent) ||
                    entityManager.HasComponent<TransformMatrix>(ent))
                    nextTransParent = ent;
            }

            /*if (allComponents.Count > 0)
            {
                Debug.LogError("Object containes components which cannot not be converted", entGo);
            }*/

            if (entGo.transform.childCount > 0)
                ConvertChildren(entityManager, entGo, nextTransParent);

            //Destroy(entGo);
        }
    }

	// Update is called once per frame
	void Update () {

	}
}
