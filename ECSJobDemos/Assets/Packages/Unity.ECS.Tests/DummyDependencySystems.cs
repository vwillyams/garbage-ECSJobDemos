using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

namespace Unity.ECS.Tests
{
	[UpdateBefore(typeof(DummySystemB))]
	[DisableAutoCreation]
	public class DummySystemA : ComponentSystem
	{
		public override void OnUpdate()
		{
		
		}
	}

	[DisableAutoCreation]
	public class DummySystemB : ComponentSystem
	{
		public override void OnUpdate()
		{

		}
	}

	[UpdateAfter(typeof(DummySystemB))]
	[DisableAutoCreation]
	public class DummySystemC : ComponentSystem
	{
		public override void OnUpdate()
		{

		}
	}

	[UpdateAfter(typeof(DummySystemB))]
	[DisableAutoCreation]
	public class DummySystemD : ComponentSystem
	{
		public override void OnUpdate()
		{

		}
	}

	[DisableAutoCreation]
	public class DummySystemE : ComponentSystem
	{
		public override void OnUpdate()
		{

		}
	}

	[UpdateAfter(typeof(DummySystemE))]
	[UpdateAfter(typeof(DummySystemB))]
	[DisableAutoCreation]
	public class DummySystemF : ComponentSystem
	{
		public override void OnUpdate()
		{

		}
	}

	[UpdateAfter(typeof(DummySystemF))]
	[DisableAutoCreation]
	public class DummySystemG : ComponentSystem
	{
		public override void OnUpdate()
		{

		}
	}

	[UpdateAfter(typeof(DummySystemG))]
	[DisableAutoCreation]
	public class DummySystemH : ComponentSystem
	{
		public override void OnUpdate()
		{

		}
	}
}
