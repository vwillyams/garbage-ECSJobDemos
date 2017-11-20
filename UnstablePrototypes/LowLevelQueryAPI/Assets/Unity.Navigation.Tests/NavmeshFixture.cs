using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

namespace Unity.Navigation.Tests
{
    public class NavmeshFixture
    {
        NavMeshData m_NavMesh;
        NavMeshDataInstance m_NavMeshInstances;
        const int k_AreaWalking = 0;
        const float k_Height = 0.3f;

        void Setup()
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
            m_NavMesh = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);

            m_NavMeshInstances = NavMesh.AddNavMeshData(m_NavMesh);
        }

        void TearDown()
        {
            m_NavMeshInstances.Remove();
        }

    }

	public class NavmeshSafety : NavmeshFixture
	{
		[Test]
		public void NewEditModeTestSimplePasses()
		{
//			var query = new 	
		}
	}
}
