using System;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.AI;
using UnityEngine.Experimental.AI;
using Unity.Collections;
using Unity.Jobs;
using System.Text.RegularExpressions;

namespace Unity.Navigation.Tests
{
	public class NavMeshQuerySafetyTests : NavMeshFixture
	{
		struct NavmeshCalculatPathJob : IJob
		{
			public NavMeshQuery pathQuery;

			public void Execute()
			{
				TestPathQuery(pathQuery);
			}
		}

		[Test]
		public void CreateAndDisposeQuery()
		{
			var query = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent, 100);
			query.Dispose();
		}

		[Test]
		public void NavMeshPathQueryWorksAfterChangingNavmesh()
		{
			var pathQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent, 100);
			for (int i = 0; i < 100; i++)
			{
				ChangeNavMesh();
				TestPathQuery(pathQuery);
			}
			pathQuery.Dispose();
		}

		[Test]
		public void JobIsCompletedBeforeMutatingNavMesh()
		{
			var job = new NavmeshCalculatPathJob();
			job.pathQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent, 100);
			var jobDep = job.Schedule();
			NavMeshWorld.GetDefaultWorld().AddDependency(jobDep);

			// @TODO: Isn't this job stuck on the jobdispatcher??? Why does this work?
			ChangeNavMesh();

			job.pathQuery.Dispose();
		}

		[Test]
		public void UnregisteredJobIsCompletedAndErrorBeforeMutatingMesh()
		{
			UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new Regex("NavMeshWorld.AddDependency"));

			var job = new NavmeshCalculatPathJob();
			job.pathQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent, 100);

			// Schedule job but forget to call NavMeshWorld.AddDependency
			job.Schedule();
			ChangeNavMesh();

			job.pathQuery.Dispose();
		}

		[Test]
		public void DisposeNavmeshQueryThenDestroyingNavmesh()
		{
			var pathQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent, 100);
			pathQuery.Dispose();
			NavMesh.RemoveAllNavmeshData();
		}

		[Test]
		public void DestroyingNavmeshWorldInvalidatesQueries()
		{
			var pathQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent, 100);
			NavMesh.RemoveAllNavmeshData();
			Assert.Throws<InvalidOperationException>(() => { pathQuery.MapLocation(Vector3.zero, Vector3.one, 0); });
			pathQuery.Dispose();
		}

		[Test]
		public void NavMeshQueryThrowsOnEmptyWorld()
		{
			Assert.Throws<System.ArgumentNullException>(() =>
			{
				new NavMeshQuery(new NavMeshWorld(), Allocator.Persistent, 100);
			});
		}

		[Test]
		public void Pathfinding_WithoutNodePool_Throws()
		{
			var queryWithoutBuffer = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent);
			var startLocation = queryWithoutBuffer.MapLocation(Vector3.zero, Vector3.one, 0);
			var endLocation = queryWithoutBuffer.MapLocation(new Vector3(5, 0, 0), Vector3.one, 0);

			int iterations;
			int pathSize;
			var path = new NativeArray<PolygonID>(100, Allocator.Persistent);
			var costs = new NativeArray<float>(32, Allocator.Persistent);

			for (var i = 0; i < costs.Length; i++)
				costs[i] = 1.0F;

			Assert.Throws<InvalidOperationException>(() => { queryWithoutBuffer.InitSlicedFindPath(startLocation, endLocation, 0, costs); });
			Assert.Throws<InvalidOperationException>(() => { queryWithoutBuffer.UpdateSlicedFindPath(100, out iterations); });
			Assert.Throws<InvalidOperationException>(() => { queryWithoutBuffer.FinalizeSlicedFindPath(out pathSize); });
			Assert.Throws<InvalidOperationException>(() => { queryWithoutBuffer.GetPathResult(path); });

			costs.Dispose();
			path.Dispose();
			queryWithoutBuffer.Dispose();
		}

		[Test]
		public void Pathfinding_WithNodePool_NoThrow()
		{
			var pathQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent, 1000);
			var startLocation = pathQuery.MapLocation(Vector3.zero, Vector3.one, 0);
			var endLocation = pathQuery.MapLocation(new Vector3(5, 0, 0), Vector3.one, 0);

			var iterations = 0;
			var pathSize = 0;
			var path = new NativeArray<PolygonID>(100, Allocator.Persistent);
			var costs = new NativeArray<float>(32, Allocator.Persistent);

			for (var i = 0; i < costs.Length; i++)
				costs[i] = 1.0F;

			Assert.DoesNotThrow(() => { pathQuery.InitSlicedFindPath(startLocation, endLocation, 0, costs); });
			Assert.DoesNotThrow(() => { pathQuery.UpdateSlicedFindPath(100, out iterations); });
			Assert.DoesNotThrow(() => { pathQuery.FinalizeSlicedFindPath(out pathSize); });
			Assert.DoesNotThrow(() => { pathQuery.GetPathResult(path); });
			Assert.NotZero(iterations);
			Assert.NotZero(pathSize);
			Assert.NotZero(path.Length);

			costs.Dispose();
			path.Dispose();
			pathQuery.Dispose();
		}
	}
}
