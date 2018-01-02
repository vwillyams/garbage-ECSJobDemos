using Unity.Mathematics;

public struct CollisionMeshData
{
    public BlobArray<float3> Vertices;
    public BlobArray<int3>   Triangles;
}