using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;
using UnityEngine.Rendering;
using Unity.Jobs;


namespace Asteriods.Client
{
    public struct LineRendererComponentData : IComponentData
    {
    }
    public class LineRenderSystem : ComponentSystem
    {
        public struct Line
        {
            public Line(float2 start, float2 end, float4 color, float width)
            {
                this.start = start;
                this.end = end;
                this.color = color;
                this.width = width;
            }
            public float2 start;
            public float2 end;
            public float4 color;
            public float width;
        }
        struct LineListComponents
        {
            public ComponentDataArray<LineRendererComponentData> line;
        }
        [Inject]
        LineListComponents m_LineListComponent;
        [Inject]
        EntityManager m_EntityManager;
		Entity m_SingletonEntity;

        NativeList<Line> m_LineList;

        // Rendering resources
        Material m_Material;
        ComputeBuffer m_ComputeBuffer;
        CommandBuffer m_CommandBuffer;

        const int MaxLines = 1024 * 1024;
         override protected void OnUpdate()
        {
            var lineList = m_LineList;
            if (Camera.main.GetCommandBuffers(CameraEvent.AfterEverything).Length == 0)
                Camera.main.AddCommandBuffer(CameraEvent.AfterEverything, m_CommandBuffer);
            if (lineList.Length > MaxLines)
            {
                Debug.LogWarning("Trying to render " + lineList.Length + " but limit is " + MaxLines);
                lineList.ResizeUninitialized(MaxLines);
            }
            NativeArray<Line> lines = lineList;
            m_Material.SetFloat("screenWidth", Screen.width);
            m_Material.SetFloat("screenHeight", Screen.height);
            m_Material.SetBuffer("lines", m_ComputeBuffer);
            m_ComputeBuffer.SetData(lines);
            m_CommandBuffer.Clear();
            m_CommandBuffer.DrawProcedural(Matrix4x4.identity, m_Material, -1, MeshTopology.Triangles, lineList.Length * 6);
            lineList.Clear();
        }

        override protected void OnCreateManager(int capacity)
        {
            var shader = Shader.Find("LineRenderer");
            if (shader == null)
            {
                Debug.Log("Wrong shader");
                m_Material = null;
                return;
            }
            m_Material = new Material(shader);
            m_ComputeBuffer = new ComputeBuffer(MaxLines, UnsafeUtility.SizeOf<Line>());
            m_CommandBuffer = new CommandBuffer();

			// Fake singleton entity
            m_SingletonEntity = m_EntityManager.CreateEntity();
            m_EntityManager.AddComponentData(m_SingletonEntity, new LineRendererComponentData());

            m_LineList = new NativeList<Line>(MaxLines, Allocator.Persistent);

            m_Material.SetBuffer("lines", m_ComputeBuffer);
            m_Material.renderQueue = (int)RenderQueue.Transparent;
        }
        override protected void OnDestroyManager()
        {
			m_EntityManager.DestroyEntity(m_SingletonEntity);
            if (m_LineList.IsCreated)
                m_LineList.Dispose();
            m_CommandBuffer.Release();
            m_ComputeBuffer.Release();
        }

        public NativeList<Line> LineList { get { return m_LineList; } }

    }
}
