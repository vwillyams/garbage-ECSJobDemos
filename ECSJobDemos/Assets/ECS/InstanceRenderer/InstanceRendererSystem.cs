using Unity.Collections;
using Unity.Mathematics;
namespace UnityEngine.ECS.Rendering
{


    /*
        public struct SimpleTransform : IComponentData
        {
            public float3   position;
            public float    scale;
            public float4   rotation;
        }
    */

    [UpdateAfter(typeof(UnityEngine.Experimental.PlayerLoop.PreLateUpdate.ParticleSystemBeginUpdateAll))]
    public class InstanceRendererSystem : ComponentSystem
	{
        // Instance renderer takes only batches of 1024
        Matrix4x4[] m_MatricesArray = new Matrix4x4[1023];

        public unsafe static void CopyMatrices(ComponentDataArray<InstanceRendererTransform> transforms, int beginIndex, int length, Matrix4x4[] outMatrices)
        {
            fixed (Matrix4x4* matricesPtr = outMatrices)
            {
                UnityEngine.Assertions.Assert.AreEqual(sizeof(Matrix4x4), sizeof(InstanceRendererTransform));
	            var matricesSlice = Unity.Collections.LowLevel.Unsafe.NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<InstanceRendererTransform>((System.IntPtr) matricesPtr, length);
                transforms.CopyTo(matricesSlice, beginIndex);
            }
        }


        public override void  OnUpdate()
		{
			base.OnUpdate();

            var uniqueRendererTypes = new NativeList<ComponentType>(10, Allocator.TempJob);
            EntityManager.GetAllUniqueSharedComponents(typeof(InstanceRenderer), uniqueRendererTypes);

            //@TODO: Do cleanup when renderer type is no longer being used...

            for (int i = 0;i != uniqueRendererTypes.Length;i++)
            {
                var uniqueType = uniqueRendererTypes[i];
                var renderer = EntityManager.GetSharedComponentData<InstanceRenderer>(uniqueType);

                var group = EntityManager.CreateComponentGroup(uniqueType, typeof(InstanceRendererTransform));

                group.CompleteDependency();

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

            uniqueRendererTypes.Dispose();
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