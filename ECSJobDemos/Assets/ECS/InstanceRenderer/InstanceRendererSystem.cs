using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System.Collections.Generic;

namespace UnityEngine.ECS.Rendering
{
	[UpdateAfter(typeof(UnityEngine.Experimental.PlayerLoop.PreLateUpdate.ParticleSystemBeginUpdateAll))]
	public class InstanceRendererSystem : ComponentSystem
	{
        // Instance renderer takes only batches of 1024
        Matrix4x4[] m_MatricesArray = new Matrix4x4[1023];

        public unsafe static void CopyMatrices(ComponentDataArray<InstanceRendererTransform> transforms, int beginIndex, int length, Matrix4x4[] outMatrices)
        {
	        // @TODO: This is only unsafe because the Unity DrawInstances API takes a Matrix4x4[] instead of NativeArray.
	        ///       And we also want the code to be really fast.
            fixed (Matrix4x4* matricesPtr = outMatrices)
            {
                UnityEngine.Assertions.Assert.AreEqual(sizeof(Matrix4x4), sizeof(InstanceRendererTransform));
	            var matricesSlice = Unity.Collections.LowLevel.Unsafe.NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<InstanceRendererTransform>(matricesPtr, sizeof(Matrix4x4), length);
	            #if ENABLE_UNITY_COLLECTIONS_CHECKS
	            Unity.Collections.LowLevel.Unsafe.NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref matricesSlice, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
	            #endif
                transforms.CopyTo(matricesSlice, beginIndex);
            }
        }

        protected override void OnUpdate()
		{
            var uniqueRendererTypes = new List<InstanceRenderer>(10);

		    var maingroup = EntityManager.CreateComponentGroup(typeof(InstanceRenderer), typeof(InstanceRendererTransform));

		    maingroup.CompleteDependency();

            EntityManager.GetAllUniqueSharedComponents(uniqueRendererTypes);

            for (int i = 0;i != uniqueRendererTypes.Count;i++)
            {
                var renderer = uniqueRendererTypes[i];

                var group = maingroup.GetVariation(renderer);

                var transforms = group.GetComponentDataArray<InstanceRendererTransform>();

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

		protected override void OnCreateManager (int capacity)
		{
			base.OnCreateManager (capacity);
		}

		protected override void OnDestroyManager ()
		{
			base.OnDestroyManager ();
		}
	}
}
