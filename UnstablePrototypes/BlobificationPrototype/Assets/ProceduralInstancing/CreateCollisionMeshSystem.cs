using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using static Unity.Mathematics.math;

public struct CollsionMeshInstance : IComponentData
{
    public BlobAssetReference<CollisionMeshData> CollisionMesh;
}

public class CreateCollisionMeshSystem : ComponentSystem
{
    unsafe struct Group
    {
        public SubtractiveComponent<CollsionMeshInstance> noMeshInstance;
        public Transform                                  transform;
        public MeshFilter                                 mesh;
        public Entity*                                    entity;
    }

    unsafe protected override void OnUpdate()
    {
        foreach (var entity in GetEntities<Group>())
        {
            var mesh = entity.mesh.sharedMesh;

            CollsionMeshInstance meshInstance;
            var collisionMesh = ConstructMeshData(mesh);
            meshInstance.CollisionMesh = collisionMesh;
            collisionMesh.Release();

            EntityManager.AddComponent(*entity.entity, meshInstance);
        }
    }

    static unsafe BlobAssetReference<CollisionMeshData> ConstructMeshData(Mesh mesh)
    {
        var allocator = new BlobAllocator(-1);

        var meshData = (CollisionMeshData*) allocator.ConstructRoot<CollisionMeshData>();

        var tris = mesh.triangles;
        allocator.Allocate(tris.Length / 3, ref meshData->Triangles);
        fixed (int* trisPtr = tris)
        {
            UnsafeUtility.MemCpy(meshData->Triangles.UnsafePtr, trisPtr, sizeof(int3) * meshData->Triangles.Length);
        }

        allocator.Allocate(mesh.vertexCount, ref meshData->Vertices);
        var vertices = mesh.vertices;
        fixed (Vector3* vertexPtr = vertices)
        {
            UnsafeUtility.MemCpy(meshData->Vertices.UnsafePtr, vertexPtr, sizeof(float3) * meshData->Vertices.Length);
        }

        return allocator.CreateBlobAssetReference<CollisionMeshData>(Allocator.Persistent);
    }

}
