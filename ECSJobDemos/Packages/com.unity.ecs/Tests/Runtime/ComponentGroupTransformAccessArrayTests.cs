using NUnit.Framework;
using Unity.Jobs;
using Unity.Collections;
using Unity.ECS;

namespace UnityEngine.ECS.Tests
{
    public class ComponentGroupTransformAccessArrayTests : ECSTestsFixture
	{
        public ComponentGroupTransformAccessArrayTests()
        {
            Assert.IsTrue(Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled, "JobDebugger must be enabled for these tests");
        }

	    public struct TransformAccessArrayTestTag : IComponentData
	    {
	    }
	    public class TransformAccessArrayTestTagComponent : ComponentDataWrapper<TransformAccessArrayTestTag> { }

	    [Test]
		public void EmptyTransformAccessArrayWorks()
	    {
	        var group = m_Manager.CreateComponentGroup(typeof(Transform), typeof(TransformAccessArrayTestTag));
	        var ta = group.GetTransformAccessArray();
			Assert.AreEqual(0, ta.Length);
	        group.Dispose();
	    }
	    [Test]
	    public void SingleItemTransformAccessArrayWorks()
	    {
	        var go = new GameObject();
	        go.AddComponent<TransformAccessArrayTestTagComponent>();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go.GetComponent<GameObjectEntity>().OnEnable();
	        var group = m_Manager.CreateComponentGroup(typeof(Transform), typeof(TransformAccessArrayTestTag));
	        var ta = group.GetTransformAccessArray();
	        Assert.AreEqual(1, ta.Length);
	        group.Dispose();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go.GetComponent<GameObjectEntity>().OnDisable();
	        Object.DestroyImmediate(go);
	    }
	    [Test]
	    public void AddAndGetNewTransformAccessArrayUpdatesContent()
	    {
	        var go = new GameObject();
	        go.AddComponent<TransformAccessArrayTestTagComponent>();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go.GetComponent<GameObjectEntity>().OnEnable();
	        var group = m_Manager.CreateComponentGroup(typeof(Transform), typeof(TransformAccessArrayTestTag));
	        var ta = group.GetTransformAccessArray();
	        Assert.AreEqual(1, ta.Length);

	        var go2 = new GameObject();
	        go2.AddComponent<TransformAccessArrayTestTagComponent>();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go2.GetComponent<GameObjectEntity>().OnEnable();
	        ta = group.GetTransformAccessArray();
	        Assert.AreEqual(2, ta.Length);

	        group.Dispose();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go.GetComponent<GameObjectEntity>().OnDisable();
	        go2.GetComponent<GameObjectEntity>().OnDisable();
	        Object.DestroyImmediate(go);
	        Object.DestroyImmediate(go2);
	    }
	    [Test]
	    // The atomic safety handle of TransformAccessArrays are not invalidated when injection changes, the array represents the transforms when you got it
	    public void AddAndUseOldTransformAccessArrayDoesNotUpdateContent()
	    {
	        var go = new GameObject();
	        go.AddComponent<TransformAccessArrayTestTagComponent>();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go.GetComponent<GameObjectEntity>().OnEnable();
	        var group = m_Manager.CreateComponentGroup(typeof(Transform), typeof(TransformAccessArrayTestTag));
	        var ta = group.GetTransformAccessArray();
	        Assert.AreEqual(1, ta.Length);

	        var go2 = new GameObject();
	        go2.AddComponent<TransformAccessArrayTestTagComponent>();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go2.GetComponent<GameObjectEntity>().OnEnable();
	        Assert.AreEqual(1, ta.Length);

	        group.Dispose();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go.GetComponent<GameObjectEntity>().OnDisable();
	        go2.GetComponent<GameObjectEntity>().OnDisable();
	        Object.DestroyImmediate(go);
	        Object.DestroyImmediate(go2);
	    }
	    [Test]
	    public void DestroyAndGetNewTransformAccessArrayUpdatesContent()
	    {
	        var go = new GameObject();
	        go.AddComponent<TransformAccessArrayTestTagComponent>();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go.GetComponent<GameObjectEntity>().OnEnable();
	        var go2 = new GameObject();
	        go2.AddComponent<TransformAccessArrayTestTagComponent>();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go2.GetComponent<GameObjectEntity>().OnEnable();

	        var group = m_Manager.CreateComponentGroup(typeof(Transform), typeof(TransformAccessArrayTestTag));
	        var ta = group.GetTransformAccessArray();
	        Assert.AreEqual(2, ta.Length);

	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go.GetComponent<GameObjectEntity>().OnDisable();
	        Object.DestroyImmediate(go);

	        ta = group.GetTransformAccessArray();
	        Assert.AreEqual(1, ta.Length);

	        group.Dispose();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go2.GetComponent<GameObjectEntity>().OnDisable();
	        Object.DestroyImmediate(go2);
	    }
	    [Test]
	    // The atomic safety handle of TransformAccessArrays are not invalidated when injection changes, the array represents the transforms when you got it
	    public void DestroyAndUseOldTransformAccessArrayDoesNotUpdateContent()
	    {
	        var go = new GameObject();
	        go.AddComponent<TransformAccessArrayTestTagComponent>();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go.GetComponent<GameObjectEntity>().OnEnable();
	        var go2 = new GameObject();
	        go2.AddComponent<TransformAccessArrayTestTagComponent>();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go2.GetComponent<GameObjectEntity>().OnEnable();

	        var group = m_Manager.CreateComponentGroup(typeof(Transform), typeof(TransformAccessArrayTestTag));
	        var ta = group.GetTransformAccessArray();
	        Assert.AreEqual(2, ta.Length);

	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go.GetComponent<GameObjectEntity>().OnDisable();
	        Object.DestroyImmediate(go);

	        Assert.AreEqual(2, ta.Length);

	        group.Dispose();
	        // Execute in edit mode is not enabled so this has to be called manually right now
	        go2.GetComponent<GameObjectEntity>().OnDisable();
	        Object.DestroyImmediate(go2);
	    }
    }
}
