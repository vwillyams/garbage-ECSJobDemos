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
			public NavMeshPathQuery pathQuery;
			public NavMeshQuery navMeshQuery;

			public void Execute()
			{
				TestPathQuery(pathQuery, navMeshQuery);
			}
		}

		[Test]
		public void CreateAndDisposeQuery()
		{
			var query = new NavMeshPathQuery(NavMeshWorld.GetDefaultWorld(), 100, Allocator.Persistent);
			query.Dispose();
		}

		[Test]
		public void NavMeshPathQueryWorksAfterChangingNavmesh()
		{
			var pathQuery = new NavMeshPathQuery(NavMeshWorld.GetDefaultWorld(), 100, Allocator.Persistent);
			var navMeshQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent);
			for (int i = 0; i < 100; i++)
			{
				ChangeNavMesh();
				TestPathQuery(pathQuery, navMeshQuery);
			}
			navMeshQuery.Dispose();
			pathQuery.Dispose();
		}

		[Test]
		public void JobIsCompletedBeforeMutatingNavMesh()
		{
			var job = new NavmeshCalculatPathJob();
			job.pathQuery = new NavMeshPathQuery(NavMeshWorld.GetDefaultWorld(), 100, Allocator.Persistent);
			job.navMeshQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent);
			var jobDep = job.Schedule();
			NavMeshWorld.GetDefaultWorld().AddDependency(jobDep);

			// @TODO: Isn't this job stuck on the jobdispatcher??? Why does this work?
			ChangeNavMesh();

			job.navMeshQuery.Dispose();
			job.pathQuery.Dispose();
		}

		[Test]
		public void UnregisteredJobIsCompletedAndErrorBeforeMutatingMesh()
		{
			UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new Regex("NavMeshWorld.AddDependency"));

			var job = new NavmeshCalculatPathJob();
			job.pathQuery = new NavMeshPathQuery(NavMeshWorld.GetDefaultWorld(), 100, Allocator.Persistent);
			job.navMeshQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent);

			// Schedule job but forget to call NavMeshWorld.AddDependency
			job.Schedule();
			ChangeNavMesh();

			job.navMeshQuery.Dispose();
			job.pathQuery.Dispose();
		}

		[Test]
		public void DisposeNavmeshQueryThenDestroyingNavmesh()
		{
			var pathQuery = new NavMeshPathQuery(NavMeshWorld.GetDefaultWorld(), 100, Allocator.Persistent);
			pathQuery.Dispose();
			NavMesh.RemoveAllNavmeshData();
		}


		public static void AssertInitSlicedFindPathThrows(NavMeshPathQuery pathQuery, NavMeshQuery navMeshQuery)
		{
			var startLocation = navMeshQuery.MapLocation(Vector3.zero, Vector3.one, 0);
			var endLocation = navMeshQuery.MapLocation(new Vector3(5, 0, 0), Vector3.one, 0);

			var costs = new NativeArray<float>(32, Allocator.Persistent);
			for (int i = 0; i < costs.Length; i++)
				costs[i] = 1.0F;

			Assert.Throws<InvalidOperationException>(() => { pathQuery.InitSlicedFindPath(startLocation, endLocation, 0, costs); });
			costs.Dispose();
		}

		[Test]
		public void DestroyingNavmeshWorldInvalidatesQueries()
		{
			var pathQuery = new NavMeshPathQuery(NavMeshWorld.GetDefaultWorld(), 100, Allocator.Persistent);
			var navMeshQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent);
			NavMesh.RemoveAllNavmeshData();
			AssertInitSlicedFindPathThrows(pathQuery, navMeshQuery);
			navMeshQuery.Dispose();
			pathQuery.Dispose();
		}

		[Test]
		public void NavMeshQueryThrowsOnEmptyWorld()
		{
			Assert.Throws<System.ArgumentNullException>(() =>
			{
				new NavMeshPathQuery(new NavMeshWorld(), 100, Allocator.Persistent);
			});
		}
	}
}
