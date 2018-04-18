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
        if (!entityManager.HasComponent(ent, typeof(Position)))
        {
            entityManager.AddComponentData(ent, new Position(go.transform.position + new Vector3(Random.Range(-0.1f, 0.1f),Random.Range(-0.1f, 0.1f),Random.Range(-0.1f, 0.1f))));
        }
    }
    public static void EnsureRotation(this EntityConvertionRule rule, EntityManager entityManager, Entity ent, GameObject go)
    {
        if (!entityManager.HasComponent(ent, typeof(Rotation)))
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
}

class MeshEntityConversionRule : EntityConvertionRule
{
    public System.Type[] ConvertedComponents()
    {
        return new System.Type[]{typeof(MeshRenderer), typeof(MeshFilter)};
    }

    public bool Convert(EntityManager entityManager, Entity ent, GameObject go)
    {
        this.EnsurePosition(entityManager, ent, go);
        //this.EnsureRotation(entityManager, ent, go);
        this.EnsureTransformMatrix(entityManager, ent, go);

        var rend = new MeshInstanceRenderer();
        MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
        MeshFilter mesh = go.GetComponent<MeshFilter>();
        rend.mesh = mesh.mesh;
        rend.material = meshRenderer.material;
        rend.castShadows = meshRenderer.shadowCastingMode;
        rend.receiveShadows = meshRenderer.receiveShadows;
        entityManager.AddSharedComponentData(ent, rend);
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

	    var converter = new MeshEntityConversionRule();
#if HAVE_BOIDS
	    var bconv = new BoidEntityConversionRule();
#endif
        for (int i = 0; i < gameObject.transform.childCount; ++i)
	    {
	        var entGo = gameObject.transform.GetChild(i).gameObject;
	        var ent = entityManager.CreateEntity();

	        converter.Convert(entityManager, ent, entGo);
#if HAVE_BOIDS
	        bconv.Convert(entityManager, ent, entGo);
#endif

	        Destroy(entGo);
	    }

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

	// Update is called once per frame
	void Update () {

	}
}
