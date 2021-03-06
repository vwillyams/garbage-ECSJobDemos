using UnityEngine;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine.Experimental.AI;
using Unity.Collections;
using Object = UnityEngine.Object;

namespace Unity.Navigation.Tests
{
    public class NavMeshFixture
    {
        public NavMeshData          m_NavMeshData;
        public NavMeshDataInstance  m_NavMeshInstance;
        const int                   k_AreaWalking = 0;
        internal const float        k_Height = 0.3f;

        [SetUp]
        public void Setup()
        {
            NavMesh.RemoveAllNavMeshData();

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

            //@TODO: Just temp workaround
            m_NavMeshInstance = NavMesh.AddNavMeshData(m_NavMeshData);
        }

        public void ChangeNavMesh()
        {
            NavMesh.RemoveNavMeshData(m_NavMeshInstance);
            m_NavMeshInstance = NavMesh.AddNavMeshData(m_NavMeshData);
        }

        public static void TestPathQuery(NavMeshQuery pathQuery)
        {
            var startLocation = pathQuery.MapLocation(Vector3.zero, Vector3.one, 0);
            var endLocation = pathQuery.MapLocation(new Vector3(5, 0, 0), Vector3.one, 0);

            Assert.AreEqual(PathQueryStatus.InProgress, pathQuery.BeginFindPath(startLocation, endLocation));
            int iterationsPerformed;
            Assert.AreEqual(PathQueryStatus.Success, pathQuery.UpdateFindPath(1000, out iterationsPerformed));
            int pathSize;
            Assert.AreEqual(PathQueryStatus.Success, pathQuery.EndFindPath(out pathSize));

            var res = new NativeArray<PolygonId>(pathSize, Allocator.Persistent);
            Assert.AreEqual(pathSize, pathQuery.GetPathResult(res));
            Assert.AreEqual(2, pathSize);

            Assert.AreEqual(startLocation.polygon, res[0]);
            Assert.AreEqual(endLocation.polygon, res[1]);

            res.Dispose();
        }

        [TearDown]
        public void TearDown()
        {
            NavMesh.RemoveNavMeshData(m_NavMeshInstance);
            Object.DestroyImmediate(m_NavMeshData);
        }
    }
}
