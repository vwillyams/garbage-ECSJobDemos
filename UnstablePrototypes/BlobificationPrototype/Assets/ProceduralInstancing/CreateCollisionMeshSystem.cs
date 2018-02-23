using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Unity.ECS;
using static Unity.Mathematics.math;

public class CreateCollisionMeshSystem : ComponentSystem
{
    [Inject] DeferredEntityChangeSystem m_DeferredChangeSystem;

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

            var collisionMeshData = (CollisionMeshData*) collisionMesh.GetUnsafePtr();
            meshInstance.Bounds = GeometryUtility.CalculateBounds(meshInstance.Transform, ref *collisionMeshData);

            //@TODO: Blob data is currently leaking... because no event when deleting components...
            m_DeferredChangeSystem.AddComponent(entity.Transform.GetComponent<GameObjectEntity>().Entity, meshInstance);
        }

        if (entities.Length != 0)
            m_DeferredChangeSystem.Update();
    }

    static unsafe BlobAssetReference<CollisionMeshData> ConstructMeshData(Mesh mesh)
    {
        var allocator = new BlobAllocator(-1);

        var meshData = (CollisionMeshData*) allocator.ConstructRoot<CollisionMeshData>();

        var tris = mesh.triangles;
        allocator.Allocate(tris.Length / 3, ref meshData->Triangles);
        fixed (int* trisPtr = tris)
        {
            UnsafeUtility.MemCpy(meshData->Triangles.GetUnsafePtr(), trisPtr, sizeof(int3) * meshData->Triangles.Length);
        }

        allocator.Allocate(mesh.vertexCount, ref meshData->Vertices);
        var vertices = mesh.vertices;
        fixed (Vector3* vertexPtr = vertices)
        {
            UnsafeUtility.MemCpy(meshData->Vertices.GetUnsafePtr(), vertexPtr, sizeof(float3) * meshData->Vertices.Length);
        }

        var assetRef = allocator.CreateBlobAssetReference<CollisionMeshData>(Allocator.Persistent);

        allocator.Dispose();

        return assetRef;
    }

}
