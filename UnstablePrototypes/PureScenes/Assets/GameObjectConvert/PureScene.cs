using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
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
            entityManager.AddComponentData(ent, new TransformMatrix());
        }
    }
    public static void EnsureTransformParent(this EntityConvertionRule rule, EntityManager entityManager, Entity ent, Entity parent)
    {
        if (!entityManager.HasComponent(ent, typeof(TransformParent)))
        {
            entityManager.AddComponentData(ent, new TransformParent(parent));
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
        if (meshRenderer == null || mesh == null)
            return false;
        this.EnsurePosition(entityManager, ent, go);
        this.EnsureRotation(entityManager, ent, go);
        //this.EnsureTransformMatrix(entityManager, ent, go);

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
            this.EnsureTransformMatrix(entityManager, meshEnt, null);
            //this.EnsureTransformParent(entityManager, meshEnt, ent);
            //entityManager.AddComponentData(meshEnt, new LocalPosition());
            //entityManager.AddComponentData(meshEnt, new LocalRotation());
            this.EnsurePosition(entityManager, meshEnt, go);
            this.EnsureRotation(entityManager, meshEnt, go);

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
	void Start ()
	{
	    var entityManager = World.Active.GetOrCreateManager<EntityManager>();

        ConvertChildren(entityManager, gameObject, new Entity());
	    /*entityManager.AddComponentData(ent, new Position(gameObject.transform.position));
	    entityManager.AddComponentData(ent, new Rotation(gameObject.transform.rotation));
	    entityManager.AddComponentData(ent, new TransformMatrix());

	    var rend = new MeshInstanceRenderer();
	    rend.mesh = gameObject.GetComponent<MeshFilter>().mesh;
	    rend.material = gameObject.GetComponent<MeshRenderer>().material;
	    entityManager.AddSharedComponentData(ent, rend);

	    Destroy(gameObject);
	    //gameObject.GetComponent<MeshRenderer>().enabled = false;
	    */
	}

    void ConvertChildren(EntityManager entityManager, GameObject go, Entity parent)
    {
        var converter = new MeshEntityConversionRule();
#if HAVE_BOIDS
	    var bconv = new BoidEntityConversionRule();
#endif
        for (int i = 0; i < go.transform.childCount; ++i)
        {
            var entGo = go.transform.GetChild(i).gameObject;
            if (!entGo.activeSelf)
                continue;
            var ent = entityManager.CreateEntity();

            /*if (go != gameObject)
            {
                entityManager.AddComponentData(ent, new TransformParent(parent));
                entityManager.AddComponentData(ent, new LocalPosition(entGo.transform.localPosition));
                entityManager.AddComponentData(ent, new LocalRotation(entGo.transform.localRotation));
            }
            else if (go.transform.childCount > 0)
            {
                converter.EnsurePosition(entityManager, ent, entGo);
                converter.EnsureRotation(entityManager, ent, entGo);
            }*/

            converter.Convert(entityManager, ent, entGo);
#if HAVE_BOIDS
	        bconv.Convert(entityManager, ent, entGo);
#endif

            if (entGo.transform.childCount > 0)
                ConvertChildren(entityManager, entGo, ent);

            Destroy(entGo);
        }
    }

	// Update is called once per frame
	void Update () {

	}
}
