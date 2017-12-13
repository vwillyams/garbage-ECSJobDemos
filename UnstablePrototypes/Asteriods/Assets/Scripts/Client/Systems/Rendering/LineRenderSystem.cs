using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;
using UnityEngine.Rendering;

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
    NativeList<Line> m_LineList;

    // Rendering resources
    Material m_Material;
    ComputeBuffer m_ComputeBuffer;
    CommandBuffer m_CommandBuffer;

    const int MaxLines = 100*1024;
    override protected void OnUpdate()
    {
        if (Camera.main.GetCommandBuffers(CameraEvent.AfterEverything).Length == 0)
            Camera.main.AddCommandBuffer(CameraEvent.AfterEverything, m_CommandBuffer);
        NativeArray<Line> lines = m_LineList;
        m_Material.SetFloat("screenWidth", Screen.width);
        m_Material.SetFloat("screenHeight", Screen.height);
        m_Material.SetBuffer("lines", m_ComputeBuffer);
        m_ComputeBuffer.SetData(lines);
        m_CommandBuffer.Clear();
        m_CommandBuffer.DrawProcedural(Matrix4x4.identity, m_Material, -1, MeshTopology.Triangles, m_LineList.Length*6);
        m_LineList.Clear();
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
        m_LineList = new NativeList<Line>(MaxLines, Allocator.Persistent);

        m_Material.SetBuffer("lines", m_ComputeBuffer);
        m_Material.renderQueue = (int)RenderQueue.Transparent;
    }
    override protected void OnDestroyManager()
    {
        if (m_LineList.IsCreated)
            m_LineList.Dispose();
        m_CommandBuffer.Release();
        m_ComputeBuffer.Release();
    }

    public NativeList<Line> LineList { get { return m_LineList; } }

}
