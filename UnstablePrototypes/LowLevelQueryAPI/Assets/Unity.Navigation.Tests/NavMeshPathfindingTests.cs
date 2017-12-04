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
        internal NativeArray<float> m_AreaCosts;

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
            var boxSource2 = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(11, k_Height, 1),
                area = k_AreaSliding,
                transform = Matrix4x4.TRS(-3 * Vector3.forward, Quaternion.identity, Vector3.one)
            };
            var sources = new List<NavMeshBuildSource> { boxSource1, boxSource2 };
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

            m_TestedAreaMask = 1 << boxSource1.area;
            m_NavMeshQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent, 2000);
            m_AreaCosts = new NativeArray<float>(32, Allocator.Persistent);
            for (var i = 0; i < m_AreaCosts.Length; i++)
                m_AreaCosts[i] = 1.0F;
        }

        [TearDown]
        public void Dispose()
        {
            m_AreaCosts.Dispose();
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
        [Test]
        public void Pathfinding_AreaCostsNotMatchingNeededSize_Throws()
        {
            var startPos = new Vector3(1.0f, 0.1f, 2.0f);
            var endPos = new Vector3(-3.0f, 1.0f, -0.1f);
            var start = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_BuildSettingsHuman.agentTypeID, m_TestedAreaMask);
            var end = m_NavMeshQuery.MapLocation(endPos, Vector3.one, m_BuildSettingsHuman.agentTypeID, m_TestedAreaMask);
            var notEnoughAreaCosts = new NativeArray<float>(31, Allocator.Temp);
            var tooManyAreaCosts = new NativeArray<float>(33, Allocator.Temp);

            Assert.Throws<ArgumentException>(() => { m_NavMeshQuery.InitSlicedFindPath(start, end, notEnoughAreaCosts); });
            Assert.Throws<ArgumentException>(() => { m_NavMeshQuery.InitSlicedFindPath(start, end, tooManyAreaCosts); });
            Assert.DoesNotThrow(() => { m_NavMeshQuery.InitSlicedFindPath(start, end, m_AreaCosts); });

            notEnoughAreaCosts.Dispose();
            tooManyAreaCosts.Dispose();
        }

        [Test]
        public void Pathfinding_BetweenDifferentTypeSurfaces_Throws()
        {
            var startPos = new Vector3(1.0f, 0.1f, 2.0f);
            var endPos = new Vector3(-3.0f, 1.0f, -0.1f);
            var humanStartLocation = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_BuildSettingsHuman.agentTypeID, m_TestedAreaMask);
            var robotEndLocation = m_NavMeshQuery.MapLocation(endPos, Vector3.one, m_BuildSettingsRobot.agentTypeID, m_TestedAreaMask);

            Assert.Throws<ArgumentException>(() => { m_NavMeshQuery.InitSlicedFindPath(humanStartLocation, robotEndLocation, m_AreaCosts); });
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

            Assert.Throws<ArgumentException>(() => { m_NavMeshQuery.InitSlicedFindPath(humanStartLocation, robotEndLocation, m_AreaCosts); });
            Assert.Throws<ArgumentException>(() => { m_NavMeshQuery.InitSlicedFindPath(robotEndLocation, humanStartLocation, m_AreaCosts); });
        }
    }
}
