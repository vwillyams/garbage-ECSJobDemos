using System;
using UnityEngine;
using NUnit.Framework;
using System.Collections.Generic;
using NUnit.Framework.Constraints;
using UnityEngine.AI;
using UnityEngine.Experimental.AI;
using Unity.Collections;
using Object = UnityEngine.Object;

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

        public void ChangeNavMesh()
        {
            m_NavMeshInstance.Remove();
	        NavMesh.AddNavMeshData(m_NavMeshData);
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
            Assert.Throws<System.ArgumentNullException>(() =>
            {
                new NavMeshPathQuery(NavMeshWorld.GetDefaultWorld(), 100, Allocator.Persistent);
            });
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
	    public void TestPathQuery(NavMeshPathQuery pathQuery)
	    {
		    var startLocation = NavMeshQuery.MapLocation(Vector3.zero, Vector3.one, 0);
		    var endLocation = NavMeshQuery.MapLocation(new Vector3(5, 0, 0), Vector3.one, 0);

		    var costs = new NativeArray<float>(32, Allocator.Persistent);
		    for (int i = 0; i < costs.Length; i++)
			    costs[i] = 1.0F;
	        
		    Assert.AreEqual(PathQueryStatus.InProgress, pathQuery.InitSlicedFindPath(startLocation, endLocation, 0, costs));
		    int iterationsPerformed;
		    Assert.AreEqual(PathQueryStatus.Success, pathQuery.UpdateSlicedFindPath(1000, out iterationsPerformed));
		    int pathSize;
		    //@TODO: Return value seems a bit weird. It can return values not on the enum when it fails? ...
		    Assert.AreEqual(PathQueryStatus.Success, pathQuery.FinalizeSlicedFindPath(out pathSize));
			
		    var res = new NativeArray<PolygonID>(pathSize, Allocator.Persistent);
		    Assert.AreEqual(pathSize, pathQuery.GetPathResult(res));
		    Assert.AreEqual(2, pathSize);
	        
		    Assert.AreEqual(startLocation.polygon, res[0].polygon);
		    Assert.AreEqual(endLocation.polygon, res[1].polygon);
	        
		    costs.Dispose();
		    res.Dispose();
	    }


	    [Test]
        public void NavMeshPathCalculationWorksAfterNavmeshChange()
	    {
		    for (int i = 0; i < 3; i++)
		    {
			    ChangeNavMesh();

			    var pathQuery = new NavMeshPathQuery(NavMeshWorld.GetDefaultWorld(), 100, Allocator.Persistent);
			    TestPathQuery(pathQuery);
			    pathQuery.Dispose();
		    }
        }

	    [Test]
	    public void ChangingNavmeshInvalidatesPathQueries()
	    {
		    var pathQuery = new NavMeshPathQuery(NavMeshWorld.GetDefaultWorld(), 100, Allocator.Persistent);
		    ChangeNavMesh();
		    Assert.Throws<InvalidOperationException>(() => { TestPathQuery(pathQuery); });
			pathQuery.Dispose();
	    }

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
