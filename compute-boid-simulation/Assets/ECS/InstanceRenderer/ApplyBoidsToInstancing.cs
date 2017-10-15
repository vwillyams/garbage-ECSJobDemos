using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using UnityEngine.ECS;
using System.Collections.Generic;

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

    public class ApplyBoidsToInstancing : JobComponentSystem
	{
        class Batch
        {
            ComponentGroup          m_Group;
            Matrix4x4[]             m_MatricesArray;

            public Batch (ComponentGroup group)
            {
                m_Group = group;

                int length = group.Length;

                m_MatricesArray = new Matrix4x4[length];
            }

            public bool IsValid()
            {
                return m_Group.Length == m_MatricesArray.Length;
            }

            public unsafe void Render(JobHandle dependency, InstanceRenderer renderer)
            {
                var transforms = m_Group.GetComponentDataArray<InstanceRendererTransform>();
                fixed (Matrix4x4* matricesPtr = m_MatricesArray)
                {
                    UnityEngine.Assertions.Assert.AreEqual(sizeof(Matrix4x4), sizeof(InstanceRendererTransform));
                    var matricesSlice = new NativeSlice<InstanceRendererTransform>(matricesPtr, m_MatricesArray.Length);

                    //@TODO: This seems like a race condition...

                    transforms.CopyTo(matricesSlice);
                }

                Graphics.DrawMeshInstanced(renderer.mesh, 0, renderer.material, m_MatricesArray, m_MatricesArray.Length, null, renderer.castShadows, renderer.receiveShadows);
            }

            public void Dispose()
            {
            }
        }

        Dictionary<ComponentType, Batch> m_ComponentToBatch = new Dictionary<ComponentType, Batch>();

		[InjectTuples]
		ComponentDataArray<InstanceRendererTransform> m_InstanceRenderers;

		public override void  OnUpdate()
		{
			base.OnUpdate();

			if (m_InstanceRenderers.Length == 0)
				return;

            var uniqueRendererTypes = new NativeList<ComponentType>(10, Allocator.TempJob);
            EntityManager.GetAllUniqueSharedComponents(typeof(InstanceRenderer), uniqueRendererTypes);

            //@TODO: Do cleanup when renderer type is no longer being used...

            for (int i = 0;i != uniqueRendererTypes.Length;i++)
            {
                var uniqueType = uniqueRendererTypes[i];
                var instanceRenderer = EntityManager.GetSharedComponentData<InstanceRenderer>(uniqueType);
                Batch batch;
                if (m_ComponentToBatch.TryGetValue(uniqueType, out batch))
                {
                    if (batch.IsValid())
                    {
                        batch.Render(GetDependency(), instanceRenderer);
                        continue;
                    }
                    else
                    {
                        batch.Dispose();
                        m_ComponentToBatch.Remove(uniqueType);
                    }
                }

                var group = EntityManager.CreateComponentGroup(uniqueType, ComponentType.Create<InstanceRendererTransform>());

                batch = new Batch(group);
                batch.Render(GetDependency(), instanceRenderer);
                m_ComponentToBatch.Add(uniqueType, batch);
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

            foreach(var batch in m_ComponentToBatch)
                batch.Value.Dispose();
            m_ComponentToBatch.Clear();
		}
	}
}