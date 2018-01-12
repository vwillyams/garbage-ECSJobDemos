using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace Data
{
    public struct RenderData : IComponentData
    {
        public Mesh Mesh;
        public Material Material;
        public Matrix4x4 Matrix;
    }
}
