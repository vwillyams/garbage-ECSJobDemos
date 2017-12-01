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
    public class NavMeshInstancesFixture
    {
        NavMeshData m_NavMeshData;
        internal NavMeshData m_NavMeshDataRobot;
        List<NavMeshDataInstance> m_NavMeshInstances;
        internal NavMeshLinkInstance m_LinkInstance;
        internal NavMeshQuery m_NavMeshQuery;
        internal const int k_AreaWalking = 0;
        internal const int k_AreaSliding = 5;
        internal Int32 m_TestedAreaMask = 0;
        internal Int32 m_TestedAgentTypeId = -10;
        internal NavMeshBuildSettings m_BuildSettingsRobot;
        internal const float k_Height = 0.3f;

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
            var settings = NavMesh.GetSettingsByID(0);
            m_NavMeshData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);

            m_BuildSettingsRobot = NavMesh.CreateSettings();
            m_NavMeshDataRobot = NavMeshBuilder.BuildNavMeshData(m_BuildSettingsRobot, sources, bounds, Vector3.zero, Quaternion.identity);

            m_NavMeshInstances = new List<NavMeshDataInstance>
            {
                NavMesh.AddNavMeshData(m_NavMeshData),
                NavMesh.AddNavMeshData(m_NavMeshData, 10 * Vector3.forward, Quaternion.identity),
                NavMesh.AddNavMeshData(m_NavMeshData, 12 * Vector3.right, Quaternion.identity)
            };

            m_TestedAgentTypeId = settings.agentTypeID;
            m_TestedAreaMask = 1 << boxSource1.area;

            m_NavMeshQuery = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.Persistent);
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

            Object.DestroyImmediate(m_NavMeshData);
        }
    }

    public class NavMeshLocationTests : NavMeshInstancesFixture
    {
        [Test]
        public void NavMeshLocation_WhenCreatedEmpty_IsInvalid()
        {
            var nmLocation = new NavMeshLocation();

            Assert.IsFalse(m_NavMeshQuery.IsValid(nmLocation));
        }

        [Test]
        public void NavMeshLocation_WhenCreatedWithPositionOutsidePolygon_Throws()
        {
            var insidePos = new Vector3(1.0f, 0.5f, 2.0f);
            var outsidePos = new Vector3(738.0f, 619.5f, 354.0f);
            var validLocation = m_NavMeshQuery.MapLocation(insidePos, Vector3.one, m_TestedAgentTypeId, m_TestedAreaMask);
            Assert.Throws<ArgumentException>(() => { var outsidePosition = new NavMeshLocation(outsidePos, validLocation.polygon); });
            Assert.Throws<ArgumentException>(() => { var nullPolygon = new NavMeshLocation(insidePos, new PolygonID()); });
            Assert.DoesNotThrow(() => { var samePosAndPoly = new NavMeshLocation(validLocation.position, validLocation.polygon); });
            Assert.DoesNotThrow(() => { var posInTheSamePoly = new NavMeshLocation( validLocation.position + new Vector3(0.1f, 0, 0), validLocation.polygon); });
        }

        [Test]
        public void MapNavMeshLocation_WithPosInside_ReturnsValidLocation()
        {
            var testPos = new Vector3(1.0f, 0.5f, 2.0f);
            var testExtents = new Vector3(0.1f, 1.0f, 0.1f);
            var nmLocation = m_NavMeshQuery.MapLocation(testPos, testExtents, m_TestedAgentTypeId, m_TestedAreaMask);

            Assert.IsTrue(m_NavMeshQuery.IsValid(nmLocation), "MapLocation() didn't return a valid location inside the NavMesh");
            Assert.AreEqual(testPos.x, nmLocation.position.x, 0.001f, "NavMeshLocation pos X not matching");
            Assert.AreEqual(k_Height, nmLocation.position.y, 0.2f, "NavMeshLocation pos Y not matching within voxel-height range");
            Assert.AreEqual(testPos.z, nmLocation.position.z, 0.001f, "NavMeshLocation pos Z not matching");
        }

        [Test]
        public void MapNavMeshLocation_WithPosOutside_ReturnsInvalidLocation()
        {
            var testPos = new Vector3(21.0f, 0.1f, 22.0f);
            var testExtents = new Vector3(1.0f, 1.0f, 1.0f);
            var nmLocation = m_NavMeshQuery.MapLocation(testPos, testExtents, m_TestedAgentTypeId, m_TestedAreaMask);

            Assert.IsFalse(m_NavMeshQuery.IsValid(nmLocation), "MapLocation() didn't return an invalid location\n"
                + " for a position outside the NavMesh" +
                " endPos=" + nmLocation.position);
        }

        [Test]
        public void MapNavMeshLocation_OnOverlappingSurfaces_ReturnsPolygonFromDesiredSurface()
        {
            Assert.AreNotEqual(0, m_BuildSettingsRobot.agentTypeID, "Expected non-zero as non-humanoid agent type id");

            var robotNavMesh = NavMesh.AddNavMeshData(m_NavMeshDataRobot);
            var robotLocation = m_NavMeshQuery.MapLocation(Vector3.zero, Vector3.one, m_BuildSettingsRobot.agentTypeID, m_TestedAreaMask);
            Assert.IsTrue(m_NavMeshQuery.IsValid(robotLocation.polygon), "MapLocation() didn't return a valid location inside the NavMesh for the specified agent type");

            var humanLocation = m_NavMeshQuery.MapLocation(Vector3.zero, Vector3.one, 0, m_TestedAreaMask);
            Assert.IsTrue(m_NavMeshQuery.IsValid(humanLocation.polygon), "MapLocation() didn't return a valid location inside the NavMesh for the Human agent");

            var agentTypeForRobotPoly = m_NavMeshQuery.GetAgentTypeIdForPolygon(robotLocation.polygon);
            Assert.AreEqual(m_BuildSettingsRobot.agentTypeID, agentTypeForRobotPoly);

            var agentTypeForHumanPoly = m_NavMeshQuery.GetAgentTypeIdForPolygon(humanLocation.polygon);
            Assert.AreNotEqual(agentTypeForHumanPoly, agentTypeForRobotPoly);

            robotNavMesh.Remove();
        }

        [Test]
        public void MoveNavMeshLocation_ToSamePos_ReturnsTheSameLocation()
        {
            var startPos = new Vector3(1.0f, 0.1f, 2.0f);
            var startLocation = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_TestedAgentTypeId, m_TestedAreaMask);
            var endLocation = m_NavMeshQuery.MoveLocation(startLocation, startPos, m_TestedAreaMask);

            Assert.IsTrue(m_NavMeshQuery.IsValid(endLocation), "MoveLocation() didn't return a valid location");
            Assert.AreEqual(startLocation.polygon, endLocation.polygon, "MoveLocation() didn't return the same poly ref when it should have");
            Assert.AreEqual(startLocation.position.x, endLocation.position.x, 0.001f, "EndLocation pos X not matching the start location position");
            Assert.AreEqual(startLocation.position.y, endLocation.position.y, 0.001f, "EndLocation pos Y not matching the start location position");
            Assert.AreEqual(startLocation.position.z, endLocation.position.z, 0.001f, "EndLocation pos Z not matching the start location position");
        }

        [Test]
        public void MoveNavMeshLocation_ToPosInside_ReturnsValidLocation()
        {
            var startPos = new Vector3(1.0f, 0.1f, 2.0f);
            var endPos = new Vector3(-3.0f, 1.0f, -0.1f);
            var startLocation = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_TestedAgentTypeId, m_TestedAreaMask);
            var endLocation = m_NavMeshQuery.MoveLocation(startLocation, endPos, m_TestedAreaMask);

            Assert.IsTrue(m_NavMeshQuery.IsValid(endLocation), "MoveLocation() didn't return a valid location inside the NavMesh");
            Assert.AreNotEqual(startLocation.polygon, endLocation.polygon, "MoveLocation() didn't return a different poly ref when it should have");
            Assert.AreEqual(endPos.x, endLocation.position.x, 0.001f, "EndLocation pos X not matching with endPos");
            Assert.AreEqual(startLocation.position.y, endLocation.position.y, 0.001f, "EndLocation pos Y not matching with endPos");
            Assert.AreEqual(endPos.z, endLocation.position.z, 0.001f, "EndLocation pos Z not matching with endPos");
        }

        [Test]
        public void MoveNavMeshLocation_ToPosOutside_ReturnsLocationAtTheEdge()
        {
            var startPos = new Vector3(1.0f, 0.1f, 2.0f);
            var endPos = new Vector3(-30.0f, 1.0f, 2.0f);
            var startLocation = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_TestedAgentTypeId, m_TestedAreaMask);
            var endLocation = m_NavMeshQuery.MoveLocation(startLocation, endPos, m_TestedAreaMask);

            Assert.IsTrue(m_NavMeshQuery.IsValid(endLocation), "MoveLocation() didn't return a valid location\n"
                + " for a destination outside the NavMesh");

            const float defaultAgentRadius = 0.5f;
            const float boxExtents = 5.0f;
            const float navMeshLeftEdgeX = -boxExtents + defaultAgentRadius;
            const float defaultVertexWidth = defaultAgentRadius / 3;
            Assert.AreEqual(navMeshLeftEdgeX, endLocation.position.x, 1.01f * defaultVertexWidth, "EndLocation pos X isn't where it should be");
            Assert.AreEqual(startLocation.position.y, endLocation.position.y, 0.001f, "EndLocation pos Y isn't where it should be");
            Assert.AreEqual(startLocation.position.z, endLocation.position.z, 0.001f, "EndLocation pos Z isn't where it should be");
        }

        [Test]
        public void MoveNavMeshLocation_ToDestinationAcrossInaccessibleArea_ReturnsLocationAtTheEdge()
        {
            var startPos = new Vector3(1.0f, 0.1f, 2.0f);
            var startLocation = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_TestedAgentTypeId, m_TestedAreaMask);

            var targetZ = new float[] { -3.0f, -4.0f };
            for (var i = 0; i < targetZ.Length; i++)
            {
                var endPosInArea = targetZ[i] * Vector3.forward + Vector3.right;
                var targetLocation = m_NavMeshQuery.MapLocation(endPosInArea, 0.01f * Vector3.one + Vector3.up, m_TestedAgentTypeId, m_TestedAreaMask);
                if (i == 0)
                {
                    Assert.IsFalse(m_NavMeshQuery.IsValid(targetLocation), "Target position is not in the inaccessible area " + targetLocation.position);
                }
                else if (i == 1)
                {
                    Assert.Less(targetZ[i], -3.5f, "Target position is not beyond the inaccessible area");
                    Assert.IsTrue(m_NavMeshQuery.IsValid(targetLocation), "Target position is not in an accessible area " + targetLocation.position);
                }

                var endLocation = m_NavMeshQuery.MoveLocation(startLocation, endPosInArea, m_TestedAreaMask);

                Assert.IsTrue(m_NavMeshQuery.IsValid(endLocation), "MoveLocation() didn't return a valid location\n"
                    + " for a target position standing on an area different than the start location" +
                    " endPos=" + endLocation.position);

                const float areaEdgeZ = -3 + 0.5f;
                Assert.AreEqual(startLocation.position.x, endLocation.position.x, 0.001f, "EndLocation pos X isn't where it should be");
                Assert.AreEqual(startLocation.position.y, endLocation.position.y, 0.001f, "EndLocation pos Y isn't where it should be");
                Assert.AreEqual(areaEdgeZ, endLocation.position.z, 0.001f, "EndLocation pos Z isn't where it should be");
            }
        }

        [Test]
        public void MoveNavMeshLocation_ToDestinationAcrossAccessibleAreas_ReturnsLocationAtTheDestination()
        {
            var startPos = new Vector3(1.0f, 0.1f, 2.0f);
            var startLocation = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_TestedAgentTypeId, NavMesh.AllAreas);

            var targetZ = new float[] { -3.0f, -4.0f };
            for (var i = 0; i < targetZ.Length; i++)
            {
                var targetPos = targetZ[i] * Vector3.forward + Vector3.right;
                var targetLocation = m_NavMeshQuery.MapLocation(targetPos, 0.01f * Vector3.one + Vector3.up, m_TestedAgentTypeId, NavMesh.AllAreas);
                Assert.IsTrue(m_NavMeshQuery.IsValid(targetLocation), "Target position is not in an accessible area: endPosInArea=" + targetPos);
                if (i == 1)
                {
                    Assert.Less(targetZ[i], -3.5f, "Target position is not beyond the inaccessible area");
                }

                var endLocation = m_NavMeshQuery.MoveLocation(startLocation, targetPos, NavMesh.AllAreas);

                Assert.IsTrue(m_NavMeshQuery.IsValid(endLocation), "MoveLocation() didn't return a valid location\n"
                    + " for a target position standing on an area different than the start location" +
                    " endPos=" + endLocation.position);

                Assert.AreEqual(targetLocation.position.x, endLocation.position.x, 0.001f, "EndLocation pos X isn't where it should be " + targetLocation.position + " actual:" + endLocation.position);
                Assert.AreEqual(targetLocation.position.y, endLocation.position.y, 0.001f, "EndLocation pos Y isn't where it should be " + targetLocation.position + " actual:" + endLocation.position);
                Assert.AreEqual(targetLocation.position.z, endLocation.position.z, 0.001f, "EndLocation pos Z isn't where it should be " + targetLocation.position + " actual:" + endLocation.position);
            }
        }

        [Test]
        public void MoveNavMeshLocation_ToDestinationOnLinkedIsland_ReturnsLocationAtTheEdge()
        {
            //setup link between islands
            var islandOffset = 12 * Vector3.right;
            var nmLink = new NavMeshLinkData
            {
                agentTypeID = m_TestedAgentTypeId,
                area = k_AreaWalking,
                bidirectional = false,
                width = 0.5f,
                startPosition = Vector3.zero,
                endPosition = islandOffset
            };

            m_LinkInstance = NavMesh.AddLink(nmLink);
            Assert.IsTrue(m_LinkInstance.valid, "Invalid link between NavMesh instances");

            var startPos = new Vector3(1.0f, 0.1f, 2.0f);
            var endPos = startPos + islandOffset;
            var startLocation = m_NavMeshQuery.MapLocation(startPos, Vector3.one, m_TestedAgentTypeId, m_TestedAreaMask);

            Assert.IsTrue(m_NavMeshQuery.IsValid(startLocation), "MapLocation() didn't return a valid START location startPos=" + startPos);

            var mappedEndLocation = m_NavMeshQuery.MapLocation(endPos, Vector3.one, m_TestedAgentTypeId, m_TestedAreaMask);
            Assert.IsTrue(m_NavMeshQuery.IsValid(mappedEndLocation), "MapLocation() didn't return a valid location\n"
                + " for a target position on another island connected with a link endPos=" + endPos);

            var endLocation = m_NavMeshQuery.MoveLocation(startLocation, endPos, m_TestedAreaMask);

            Assert.IsTrue(m_NavMeshQuery.IsValid(endLocation), "MoveLocation() didn't return a valid location\n"
                + " at the edge of the main island\n"
                + " for a target position on another island that's connected with a link");

            const float defaultAgentRadius = 0.5f;
            const float boxExtents = 5.0f;
            const float navMeshLeftEdgeX = boxExtents - defaultAgentRadius;
            const float defaultVertexWidth = defaultAgentRadius / 3;

            Assert.AreEqual(navMeshLeftEdgeX, endLocation.position.x, 1.01f * defaultVertexWidth, "EndLocation pos X isn't at the right edge of NavMesh");
            Assert.AreEqual(startLocation.position.y, endLocation.position.y, 0.001f, "EndLocation pos Y isn't at the right edge of NavMesh");
            Assert.AreEqual(startLocation.position.z, endLocation.position.z, 0.001f, "EndLocation pos Z isn't at the right edge of NavMesh");
        }
    }
}
