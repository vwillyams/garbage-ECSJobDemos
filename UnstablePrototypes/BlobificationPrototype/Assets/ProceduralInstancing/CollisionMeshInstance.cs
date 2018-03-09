using Unity.Mathematics;
using Unity.Entities;

public struct CollisionMeshInstance : IComponentData
{
    public Bounds                                Bounds;
    public float4x4                              Transform;
    public BlobAssetReference<CollisionMeshData> CollisionMesh;
}
