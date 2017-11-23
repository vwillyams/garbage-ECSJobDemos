using UnityEngine;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine.Experimental.AI;
using Unity.Collections;
using Object = UnityEngine.Object;

namespace Unity.Navigation.Tests
{
   

	public class NavMeshTests : NavMeshFixture
	{
		[Test]
		[Ignore("FIXME")]
		public void RemoveNavmeshInstanceTwiceLogsError()
		{
			UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Failed...");
			NavMesh.RemoveNavMeshData(m_NavMeshInstance);
			NavMesh.RemoveNavMeshData(m_NavMeshInstance);
		}
	    
	
		[Test]
		public void NavMesh_Exists()
		{
			//@TODO: We have two different NavMeshHit... THATS BAD
			UnityEngine.AI.NavMeshHit hit;
			var center = new Vector3(0, k_Height, 0);
			var found = NavMesh.SamplePosition(center, out hit, 1, NavMesh.AllAreas);
			Assert.IsTrue(found, string.Format("NavMesh was not found at position {0}.", center));
		}
	}
}
