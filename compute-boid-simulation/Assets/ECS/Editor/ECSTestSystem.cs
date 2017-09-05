using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.Jobs;
using UnityEngine.ECS;
using UnityEngine.Collections;

namespace UnityEngine.ECS.Tests
{
	public class PureEcsTestSystem : ComponentSystem
	{
		[InjectTuples]
		public ComponentDataArray<EcsTestData> m_Data;

		[InjectTuples]
		public EntityArray m_Entities;

		public void OnUpdate() { base.OnUpdate (); }
	}

	public class EcsTestAndTransformArraySystem : ComponentSystem
	{
		[InjectTuples]
		public ComponentDataArray<EcsTestData> m_Data;

		[InjectTuples]
		public TransformAccessArray m_Transforms;

		public void OnUpdate() { base.OnUpdate (); }
	}
	public class EcsTestAndTransformComponentSystem : ComponentSystem
	{
		[InjectTuples]
		public ComponentDataArray<EcsTestData> m_Data;

		[InjectTuples]
		public ComponentArray<Transform> m_Transforms;

		public void OnUpdate() { base.OnUpdate (); }
	}

	public class PureReadOnlySystem : ComponentSystem
	{
		[InjectTuples]
		[ReadOnlyAttribute]
		public ComponentDataArray<EcsTestData> m_Data;

		public void OnUpdate() { base.OnUpdate (); }
	}

	public class GroupChangeSystem : ComponentSystem, IEntityGroupChange
	{
		[InjectTuples]
		public ComponentDataArray<EcsTestData> m_Data;

		List<int> m_OnAddElements = new List<int> ();
		List<int> m_OnRemoveSwapBack = new List<int> ();

		public void OnAddElements (int count)
		{
			m_OnAddElements.Add (count);
		}

		public void OnRemoveSwapBack (int index)
		{
			m_OnRemoveSwapBack.Add (index);
		}

		public void ExpectDidAddElements(int count)
		{
			Assert.AreEqual (1, m_OnAddElements.Count);
			Assert.AreEqual (0, m_OnRemoveSwapBack.Count);

			Assert.AreEqual (count, m_OnAddElements.Count);

			m_OnAddElements.Clear ();
		}

		public void ExpectDidRemoveSwapBack(int element)
		{
			Assert.AreEqual (1, m_OnRemoveSwapBack.Count);
			Assert.AreEqual (0, m_OnAddElements.Count);

			Assert.AreEqual (element, m_OnRemoveSwapBack[0]);

			m_OnRemoveSwapBack.Clear ();
		}
	}


}