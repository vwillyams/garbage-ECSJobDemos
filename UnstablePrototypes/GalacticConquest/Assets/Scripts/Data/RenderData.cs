using System;
using Unity.ECS;
using UnityEngine;

namespace Data
{
    public struct RenderData : IComponentData
    {
        public Mesh Mesh;
        public Material Material;
        public Matrix4x4 Matrix;
    }
}
