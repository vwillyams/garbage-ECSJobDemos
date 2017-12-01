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
		protected override void OnUpdate()
		{
		
		}
	}

	[DisableAutoCreation]
	public class DummySystemB : ComponentSystem
	{
		protected override void OnUpdate()
		{

		}
	}

	[UpdateAfter(typeof(DummySystemB))]
	[DisableAutoCreation]
	public class DummySystemC : ComponentSystem
	{
		protected override void OnUpdate()
		{

		}
	}

	[UpdateAfter(typeof(DummySystemB))]
	[DisableAutoCreation]
	public class DummySystemD : ComponentSystem
	{
		protected override void OnUpdate()
		{

		}
	}

	[DisableAutoCreation]
	public class DummySystemE : ComponentSystem
	{
		protected override void OnUpdate()
		{

		}
	}

	[UpdateAfter(typeof(DummySystemE))]
	[UpdateAfter(typeof(DummySystemB))]
	[DisableAutoCreation]
	public class DummySystemF : ComponentSystem
	{
		protected override void OnUpdate()
		{

		}
	}

	[UpdateAfter(typeof(DummySystemF))]
	[DisableAutoCreation]
	public class DummySystemG : ComponentSystem
	{
		protected override void OnUpdate()
		{

		}
	}

	[UpdateAfter(typeof(DummySystemG))]
	[DisableAutoCreation]
	public class DummySystemH : ComponentSystem
	{
		protected override void OnUpdate()
		{

		}
	}
}
