using UnityEngine;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine.Experimental.AI;
using Unity.Collections;

namespace Unity.Navigation.Tests
{
    public class NavmeshFixture
    {
        NavMeshData 			m_NavMeshData;
        NavMeshDataInstance 	m_NavMeshInstance;
        const int 				k_AreaWalking = 0;
        internal const float 	k_Height = 0.3f;

        [SetUp]
        public void Setup()
        {
            var boxSource1 = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(10, k_Height, 10),
                area = k_AreaWalking,
                transform = Matrix4x4.identity
            };
            var sources = new List<NavMeshBuildSource> { boxSource1 };
            var bounds = new Bounds(Vector3.zero, 100.0f * Vector3.one);
            var settings = NavMesh.GetSettingsByID(0);
            m_NavMeshData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);

            m_NavMeshInstance = NavMesh.AddNavMeshData(m_NavMeshData);
        }

        [TearDown]
        public void TearDown()
        {
            m_NavMeshInstance.Remove();
            Object.DestroyImmediate(m_NavMeshData);
        }
    }

	public class NavmeshPathQueryInvalidSetup
	{
		[Test]
		public void CreateAndDisposeQueryOnEmptyDefaultWorld()
		{
			var query = new NavMeshPathQuery(NavMeshWorld.GetDefaultWorld(), 100, Allocator.Persistent);
			query.Dispose();
		}

		[Test]
		public void CreateAndDisposeQueryOnEmptyNullWorld()
		{
			Assert.Throws<System.ArgumentNullException>(() =>
			{
				new NavMeshPathQuery(new NavMeshWorld(), 100, Allocator.Persistent);
			});
		}
	}

    public class NavMeshSanity : NavmeshFixture
    {
		[Test]
		public void CreateAndDisposeQuery()
		{
			var query = new NavMeshPathQuery(NavMeshWorld.GetDefaultWorld(), 100, Allocator.Persistent);
			query.Dispose();
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
