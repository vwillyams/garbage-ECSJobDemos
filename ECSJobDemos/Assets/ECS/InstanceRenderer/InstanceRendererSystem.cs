using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.Rendering
{
	[UpdateAfter(typeof(UnityEngine.Experimental.PlayerLoop.PreLateUpdate.ParticleSystemBeginUpdateAll))]
	public class InstanceRendererSystem : ComponentSystem
	{
        // Instance renderer takes only batches of 1023
        Matrix4x4[] m_MatricesArray = new Matrix4x4[1023];

	    // This is the ugly bit, necessary until Graphics.DrawMeshInstanced supports NativeArrays pulling the data in from a job.
        public unsafe static void CopyMatrices(ComponentDataArray<TransformMatrix> transforms, int beginIndex, int length, Matrix4x4[] outMatrices)
        {
	        // @TODO: This is using unsafe code because the Unity DrawInstances API takes a Matrix4x4[] instead of NativeArray.
	        // We want to use the ComponentDataArray.CopyTo method
	        // because internally it uses memcpy to copy the data,
	        // if the nativeslice layout matches the layout of the component data. It's very fast...
            fixed (Matrix4x4* matricesPtr = outMatrices)
            {
                UnityEngine.Assertions.Assert.AreEqual(sizeof(Matrix4x4), sizeof(TransformMatrix));
	            var matricesSlice = Unity.Collections.LowLevel.Unsafe.NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<TransformMatrix>(matricesPtr, sizeof(Matrix4x4), length);
	            #if ENABLE_UNITY_COLLECTIONS_CHECKS
	            Unity.Collections.LowLevel.Unsafe.NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref matricesSlice, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
	            #endif
                transforms.CopyTo(matricesSlice, beginIndex);
            }
        }

        protected override void OnUpdate()
		{
            // We want to find all InstanceRenderer & TransformMatrix combinations and render them
		    var maingroup = EntityManager.CreateComponentGroup(typeof(InstanceRenderer), typeof(TransformMatrix));
		    // We didn't declare the ComponentGroup via injection so we need to manually
		    // Complete any jobs that are writing to TransformMatrices
		    maingroup.CompleteDependency();

		    // We want to iterate over all unique InstanceRenderer shared component data,
		    // that are attached to any entities in the world
		    var uniqueRendererTypes = new List<InstanceRenderer>(10);
            EntityManager.GetAllUniqueSharedComponents(uniqueRendererTypes);

            for (int i = 0;i != uniqueRendererTypes.Count;i++)
            {
                // For each unique InstanceRenderer data, we want to get all entities with a TransformMatrix
                // SharedComponentData gurantees that all those entities are packed togehter in a chunk with linear memory layout.
                // As a result the copy of the matrices out is internally done via memcpy. 
                var renderer = uniqueRendererTypes[i];
                var group = maingroup.GetVariation(renderer);
                var transforms = group.GetComponentDataArray<TransformMatrix>();

                // Graphics.DrawMeshInstanced has a set of limitations that are not optimal for working with the ECS.
                // We will adjust this API in the future to make it easy to fill in the buffers from jobs via Native containers directly.
                // For now, we have to copy our data into Matrix4x4[] with a specific upper limit of how many instances we can render in one batch.
                // So we just have a for loop here, representing each Graphics.DrawMeshInstanced batch
                int beginIndex = 0;
                while (beginIndex < transforms.Length)
                {
                    int length = math.min(m_MatricesArray.Length, transforms.Length - beginIndex);
                    CopyMatrices(transforms, beginIndex, length, m_MatricesArray);
                    Graphics.DrawMeshInstanced(renderer.mesh, 0, renderer.material, m_MatricesArray, length, null, renderer.castShadows, renderer.receiveShadows);

                    beginIndex += length;
                }

                group.Dispose();
            }

		    maingroup.Dispose();
		}
	}
}
