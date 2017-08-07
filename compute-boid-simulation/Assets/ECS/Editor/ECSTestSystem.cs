using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.Jobs;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Tests
{
	public class PureEcsTestSystem : ComponentSystem
	{
		[InjectTuples]
		public ComponentDataArray<EcsTestData> m_Data;

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

}