using UnityEngine;
using UnityEngine.ECS;

public enum TimesliceBehaviour
{
	// This is the default.
	// If the scene load is to be time sliced,
	// then TimesliceBehaviour.None will be awoken in the final integration step,
	// thus not timesliced. Hence other components will be timeslice integrated before them.
	None,

	// It is ok to OnCreate and OnActivate during timeslice but not OnEnable / OnUpdate
	// until the final integration step.
	// For example a MeshRenderer could prepare all its data in OnCreate / OnActivate
	// but would not yet register itself in the RendererScene
	// and thus get rendered until the final integration step.
	CreateAndActivate,

	/// It is ok to OnActivate and OnEnable and thus also start pumping OnUpdate for the behaviour
	/// during timesliced integration.
	/// OnUpdate will start being called after the object has completed time sliced integration.
	CreateActivateEnableAndStartUpdate
}

class AllowTimesliceAttribute : System.Attribute
{
	public AllowTimesliceAttribute(TimesliceBehaviour behaviour) { }
}


// NOTE: if params.timeSlice = false; for the scene load
//       then this attribute will have no effect.
[AllowTimeslice (TimesliceBehaviour.CreateAndActivate)]
class SetupRigidbody : ScriptBehaviour
{
	// Unity now knows that rigidbody must be awoken before rotator is OnCreate called.
	// Due to the declared dependency, rigidbody will have been fully constructed
	// before OnCreate is called.
	// Additionally the default behaviour for [InjectDependency] on behaviours is that it will 
	// GetComponent the rigidbody, since it is declarative Unity
	// can safely do this on the loading thread from C++ code.
	[InjectDependency]
	Rigidbody m_Rigidbody;

	override protected void OnCreate()
	{
		m_Rigidbody.mass = 5;
	}

	override public void OnUpdate()
	{
		m_Rigidbody.AddForce(1, 2, 3);
	}
}

#if false
var params = new SceneLoadParameters();

// Enables timeslicing to happen for components that support it.
// Builtin components will define their own behaviour.
// For example MeshRenderer, would get the full OnCreate, OnActivate sequence,
// but it would effectively remain enabled = false;
// until the final integration step which now only has to .enable all MeshRenderers.
params.timeSlice = true; 


// allows using a chunk allocator for the scene. 
// This implies that no memory will be unloaded for scene objects until the scene is unloaded. 
// When the scene is unloaded and any objects are left over (hide and dont save etc)
// an error message will be thrown.
params.useChunkAllocator = true;

SceneManager.LoadScene("streamedScene", params);

#endif