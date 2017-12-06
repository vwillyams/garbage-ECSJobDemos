using System;
using UnityEngine;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.AI;
using UnityEngine.Experimental.AI;
using Object = UnityEngine.Object;

namespace Unity.Navigation.Tests
{
    public class NavMeshInstancesForMultipleAgentsFixture
    {
        NavMeshData m_NavMeshDataHuman;
        internal NavMeshData m_NavMeshDataRobot;
        List<NavMeshDataInstance> m_NavMeshInstances;
        internal NavMeshLinkInstance m_LinkInstance;
        internal NavMeshQuery m_NavMeshQuery;
        internal const int k_AreaWalking = 0;
        internal const int k_AreaSliding = 5;
        internal Int32 m_TestedAreaMask = 0;
        internal const float k_Height = 0.3f;
        internal NavMeshBuildSettings m_BuildSettingsHuman;
        internal NavMeshBuildSettings m_BuildSettingsRobot;

        [SetUp]
        public void Setup()
        {
            var ground = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(10, k_Height, 10),
                area = k_AreaWalking,
                transform = Matrix4x4.identity
            };
            var areaPatch1 = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                size = new Vector3(11, 1, 1),
                area = k_AreaSliding,
                transform = Matrix4x4.TRS(-3 * Vector3.forward, Quaternion.identity, Vector3.one)
            };
            var areaPatch2 = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                size = new Vector3(1, 1, 1),
                area = k_AreaSliding,
                transform = Matrix4x4.TRS(new Vector3(2f, 0, 2f), Quaternion.identity, Vector3.one)
            };
            var areaPatch3 = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                size = new Vector3(0.2f, 1, 2f),
                area = k_AreaSliding,
                transform = Matrix4x4.TRS(new Vector3(1.5f, 0, 2f), Quaternion.identity, Vector3.one)
            };
            var areaPatch4 = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                size = new Vector3(0.2f, 1, 2f),
                area = k_AreaSliding,
                transform = Matrix4x4.TRS(new Vector3(2.5f, 0, 2f), Quaternion.identity, Vector3.one)
            };
            var sources = new List<NavMeshBuildSource> { ground, areaPatch1, areaPatch2, areaPatch3, areaPatch4 };
            var bounds = new Bounds(Vector3.zero, 100.0f * Vector3.one);

            m_BuildSettingsHuman = NavMesh.GetSettingsByID(0);
            m_NavMeshDataHuman = NavMeshBuilder.BuildNavMeshData(m_BuildSettingsHuman, sources, bounds, Vector3.zero, Quaternion.identity);

            m_BuildSettingsRobot = NavMesh.CreateSettings();
            m_BuildSettingsRobot.agentRadius = 0.9f;
            Assert.AreNotEqual(0, m_BuildSettingsRobot.agentTypeID, "Expected non-zero as non-humanoid agent type id");
            m_NavMeshDataRobot = NavMeshBuilder.BuildNavMeshData(m_BuildSettingsRobot, sources, bounds, Vector3.zero, Quaternion.identity);

            m_NavMeshInstances = new List<NavMeshDataInstance>
            {
                NavMesh.AddNavMeshData(m_NavMeshDataHuman),
                NavMesh.AddNavMeshData(m_NavMeshDataHuman, 10 * Vector3.forward, Quaternion.identity),
                NavMesh.AddNavMeshData(m_NavMeshDataHuman, 12 * Vector3.right, Quaternion.identity),

                NavMesh.AddNavMeshData(m_NavMeshDataRobot),
                NavMesh.AddNavMeshData(m_NavMeshDataRobot, 10 * Vector3.forward, Quaternion.identity),
            };

            m_TestedAreaMask = 1 << ground.area;
            m_NavMeshQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent, 2000);
        }

        [TearDown]
        public void Dispose()
        {
            m_NavMeshQuery.Dispose();
            foreach (var nmInstance in m_NavMeshInstances)
            {
                nmInstance.Remove();
            }
            m_NavMeshInstances.Clear();
            m_LinkInstance.Remove();

            NavMesh.RemoveSettings(m_BuildSettingsRobot.agentTypeID);

            Object.DestroyImmediate(m_NavMeshDataHuman);
            Object.DestroyImmediate(m_NavMeshDataRobot);
        }
    }

    public class NavMeshPathfindingTests : NavMeshInstancesForMultipleAgentsFixture
    {
        void TestPathfindingOverTwoAreaTypes(int expectedPathSize, string failMsg, NativeArray<float> costs = new NativeArray<float>())
        {
            var startPos = new Vector3(2f, 0f, 1.3f);
            var endPosBeyondDifferentArea = new Vector3(2f, 0f, 2.7f);
            var startLoc = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_BuildSettingsHuman.agentTypeID, m_TestedAreaMask);
            var endLoc = m_NavMeshQuery.MapLocation(endPosBeyondDifferentArea, Vector3.one, m_BuildSettingsHuman.agentTypeID, m_TestedAreaMask);

            PathQueryStatus status;
            if (costs.Length == 0)
            {
                status = m_NavMeshQuery.InitSlicedFindPath(startLoc, endLoc, NavMesh.AllAreas);
            }
            else
            {
                status = m_NavMeshQuery.InitSlicedFindPath(startLoc, endLoc, NavMesh.AllAreas, costs);
            }
            Assert.That(status, Is.EqualTo(PathQueryStatus.InProgress));

            int iter;
            status = m_NavMeshQuery.UpdateSlicedFindPath(100, out iter);
            Assert.That(status, Is.EqualTo(PathQueryStatus.Success));

            int pathSize;
            status = m_NavMeshQuery.FinalizeSlicedFindPath(out pathSize);
            Assert.That(status, Is.EqualTo(PathQueryStatus.Success));
            Assert.That(pathSize, Is.EqualTo(expectedPathSize), failMsg);

            var path = new NativeArray<PolygonID>(pathSize, Allocator.Temp);
            var pathResultSize = m_NavMeshQuery.GetPathResult(path);
            Assert.That(pathResultSize, Is.EqualTo(pathSize));

            path.Dispose();
        }

        [Test]
        public void Pathfinding_UsingExpensiveAreaCostByDefault_ReturnsLongerPath()
        {
            var originalCost = NavMesh.GetAreaCost(k_AreaSliding);

            // increase a lot the default cost of the area
            NavMesh.SetAreaCost(k_AreaSliding, 50);
            const int pathSizeAroundArea = 7;
            const string msgWhenAreaNotAvoided = "The path that goes around the costly area must have a bend " +
                "instead of it being just the shortest string of polygons from start to the end.";
            TestPathfindingOverTwoAreaTypes(pathSizeAroundArea, msgWhenAreaNotAvoided);

            NavMesh.SetAreaCost(k_AreaSliding, originalCost);
        }

        [Test]
        public void Pathfinding_UsingLowAreaCostByDefault_ReturnsShortestPath()
        {
            var originalCost = NavMesh.GetAreaCost(k_AreaSliding);

            // reduce to minimum any default cost for that area
            NavMesh.SetAreaCost(k_AreaSliding, 1);
            const int pathSizeStaightThroughArea = 3;
            const string msgWhenPathNotStraight = "The path should cut through all areas in one straight line from start to the end.";
            TestPathfindingOverTwoAreaTypes(pathSizeStaightThroughArea, msgWhenPathNotStraight);

            NavMesh.SetAreaCost(k_AreaSliding, originalCost);
        }

        [Test]
        public void Pathfinding_UsingMinimumAreaCostOverride_ReturnsShortestPath()
        {
            var originalCost = NavMesh.GetAreaCost(k_AreaSliding);

            // increase a lot the default cost of the area
            NavMesh.SetAreaCost(k_AreaSliding, 50);

            // provide a custom list of costs
            var areaCosts = new NativeArray<float>(32, Allocator.Temp);
            for (var i = 0; i < areaCosts.Length; i++)
                areaCosts[i] = 1.0F;

            const int pathSizeStaightThroughArea = 3;
            const string msgWhenPathNotStraight = "The path should cut through all areas in one straight line from start to the end.";
            TestPathfindingOverTwoAreaTypes(pathSizeStaightThroughArea, msgWhenPathNotStraight, areaCosts);

            areaCosts.Dispose();
            NavMesh.SetAreaCost(k_AreaSliding, originalCost);
        }

        [Test]
        public void Pathfinding_UsingInvalidAreaCost_Throws()
        {
            var startPos = new Vector3(1.0f, 0.1f, 2.0f);
            var endPos = new Vector3(-3.0f, 1.0f, -0.1f);
            var start = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_BuildSettingsHuman.agentTypeID, m_TestedAreaMask);
            var end = m_NavMeshQuery.MapLocation(endPos, Vector3.one, m_BuildSettingsHuman.agentTypeID, m_TestedAreaMask);
            var areaCosts = new NativeArray<float>(32, Allocator.Temp);
            for (var i = 0; i < areaCosts.Length; i++)
                areaCosts[i] = 1.0F;

            Assert.DoesNotThrow(() => { m_NavMeshQuery.InitSlicedFindPath(start, end, NavMesh.AllAreas, areaCosts); });

            areaCosts[9] = 0;
            Assert.Throws<ArgumentException>(() => { m_NavMeshQuery.InitSlicedFindPath(start, end, NavMesh.AllAreas, areaCosts); });

            areaCosts.Dispose();
        }

        [Test]
        public void Pathfinding_AreaCostsNotMatchingCount_Throws()
        {
            var startPos = new Vector3(1.0f, 0.1f, 2.0f);
            var endPos = new Vector3(-3.0f, 1.0f, -0.1f);
            var start = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_BuildSettingsHuman.agentTypeID, m_TestedAreaMask);
            var end = m_NavMeshQuery.MapLocation(endPos, Vector3.one, m_BuildSettingsHuman.agentTypeID, m_TestedAreaMask);
            var notEnoughAreaCosts = new NativeArray<float>(31, Allocator.Temp);
            var tooManyAreaCosts = new NativeArray<float>(33, Allocator.Temp);
            var allNeededAreaCosts = new NativeArray<float>(32, Allocator.Temp);
            for (var i = 0; i < allNeededAreaCosts.Length; i++)
                allNeededAreaCosts[i] = 1.0F;

            Assert.Throws<ArgumentException>(() => { m_NavMeshQuery.InitSlicedFindPath(start, end, NavMesh.AllAreas, notEnoughAreaCosts); });
            Assert.Throws<ArgumentException>(() => { m_NavMeshQuery.InitSlicedFindPath(start, end, NavMesh.AllAreas, tooManyAreaCosts); });
            Assert.DoesNotThrow(() => { m_NavMeshQuery.InitSlicedFindPath(start, end, NavMesh.AllAreas, allNeededAreaCosts); });

            notEnoughAreaCosts.Dispose();
            tooManyAreaCosts.Dispose();
            allNeededAreaCosts.Dispose();
        }

        [Test]
        public void Pathfinding_BetweenDifferentSurfaceTypes_Throws()
        {
            var startPos = new Vector3(1.0f, 0.1f, 2.0f);
            var endPos = new Vector3(-3.0f, 1.0f, -0.1f);
            var humanStartLocation = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_BuildSettingsHuman.agentTypeID, m_TestedAreaMask);
            var robotEndLocation = m_NavMeshQuery.MapLocation(endPos, Vector3.one, m_BuildSettingsRobot.agentTypeID, m_TestedAreaMask);

            Assert.Throws<ArgumentException>(() => { m_NavMeshQuery.InitSlicedFindPath(humanStartLocation, robotEndLocation); });
        }

        [Test]
        public void Pathfinding_OnInactiveSurface_Throws()
        {
            var startPos = new Vector3(1.0f, 0.1f, 2.0f);
            var endPos = 12 * Vector3.right;
            var temporaryNavMesh = NavMesh.AddNavMeshData(m_NavMeshDataRobot, endPos, Quaternion.identity);

            var humanStartLocation = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_BuildSettingsHuman.agentTypeID, m_TestedAreaMask);
            var robotEndLocation = m_NavMeshQuery.MapLocation(endPos, Vector3.one, m_BuildSettingsRobot.agentTypeID, m_TestedAreaMask);

            Assert.IsTrue(m_NavMeshQuery.IsValid(humanStartLocation));
            Assert.IsTrue(m_NavMeshQuery.IsValid(robotEndLocation));

            temporaryNavMesh.Remove();

            Assert.IsFalse(m_NavMeshQuery.IsValid(robotEndLocation));

            Assert.Throws<ArgumentException>(() => { m_NavMeshQuery.InitSlicedFindPath(humanStartLocation, robotEndLocation); });
            Assert.Throws<ArgumentException>(() => { m_NavMeshQuery.InitSlicedFindPath(robotEndLocation, humanStartLocation); });
        }
    }
}
