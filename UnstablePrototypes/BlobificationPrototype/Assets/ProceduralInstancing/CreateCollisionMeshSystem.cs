using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;

public class CreateCollisionMeshSystem : ComponentSystem
{
    unsafe struct Group
    {
        public SubtractiveComponent<CollisionMeshInstance> NoMeshInstance;
        public Transform                                  Transform;
        public MeshFilter                                 Mesh;

        //@TODO: Write test for injecting Entity struct into Group...
//        public Entity*                                    Entity;
    }


    unsafe protected override void OnUpdate()
    {
        var entities = GetEntities<Group>();
        foreach (var entity in GetEntities<Group>())
        {
            var mesh = entity.Mesh.sharedMesh;

            CollisionMeshInstance meshInstance;
            var collisionMesh = ConstructMeshData(mesh);
            meshInstance.CollisionMesh = collisionMesh;
            meshInstance.Transform = entity.Transform.localToWorldMatrix;

            meshInstance.Bounds = GeometryUtility.CalculateBounds(meshInstance.Transform, ref collisionMesh.Value);

            PostUpdateCommands.AddComponent(entity.Transform.GetComponent<GameObjectEntity>().Entity, meshInstance);
        }
    }

    static unsafe BlobAssetReference<CollisionMeshData> ConstructMeshData(Mesh mesh)
    {
        var allocator = new BlobAllocator(-1);

        ref var meshData = ref allocator.ConstructRoot<CollisionMeshData>();

        var tris = mesh.triangles;
        allocator.Allocate(tris.Length / 3, ref meshData.Triangles);
        fixed (int* trisPtr = tris)
        {
            UnsafeUtility.MemCpy(meshData.Triangles.GetUnsafePtr(), trisPtr, sizeof(int3) * meshData.Triangles.Length);
        }

        allocator.Allocate(mesh.vertexCount, ref meshData.Vertices);
        var vertices = mesh.vertices;
        fixed (Vector3* vertexPtr = vertices)
        {
            UnsafeUtility.MemCpy(meshData.Vertices.GetUnsafePtr(), vertexPtr, sizeof(float3) * meshData.Vertices.Length);
        }

        var assetRef = allocator.CreateBlobAssetReference<CollisionMeshData>(Allocator.Persistent);

        allocator.Dispose();

        return assetRef;
    }

}
