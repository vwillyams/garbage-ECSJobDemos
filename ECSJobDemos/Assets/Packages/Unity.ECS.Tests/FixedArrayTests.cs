using UnityEngine.ECS;
using NUnit.Framework;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.ECS.Tests
{
	public class FixedArrayTests : ECSTestsFixture
	{
		[Test]
		[Ignore("TODO")]
		public void AddingComponentWithJustIntButNotFixedArrayThrows()
		{
		}
		
		[Test]
		[Ignore("TODO")]
		public void GetComponentDataFixedArrayAgainstNotArrayThrows()
		{
		}

		[Test]
		[Ignore("TODO")]
		public void GetComponentDataAgainstArrayThrows()
		{
		}

		[Test]
		[Ignore("TODO")]
		public void RemoveComponentWithDifferentArraySizeWorks()
		{
		}
		
		[Test]
		public void FixedArrayAddRemoveComponent()
		{
			var entity = m_Manager.CreateEntity();
			m_Manager.AddComponent(entity, ComponentType.FixedArray(typeof(int), 11));

			Assert.IsTrue(m_Manager.HasComponent(entity, ComponentType.FixedArray(typeof(int), 11)));			
			Assert.IsTrue(m_Manager.HasComponent(entity, typeof(int)));			

			var array = m_Manager.GetComponentFixedArray<int>(entity);
			
			Assert.AreEqual(11, array.Length);
			array[7] = 5;
			Assert.AreEqual(5, array[7]);
			
			m_Manager.RemoveComponent(entity, ComponentType.FixedArray(typeof(int), 11));
			
			Assert.IsFalse(m_Manager.HasComponent(entity, ComponentType.FixedArray(typeof(int), 11)));			
			Assert.IsFalse(m_Manager.HasComponent(entity, typeof(int)));			
			Assert.Throws<ArgumentException>(() => { m_Manager.GetComponentFixedArray<int>(entity); });			
		}
		

		[Test]
        public void CreateAndDestroyFixedArray()
        {
            var entity64 = m_Manager.CreateEntity(ComponentType.FixedArray(typeof(int), 64));
	        var entity10 = m_Manager.CreateEntity(ComponentType.FixedArray(typeof(int), 10));

            var group = m_Manager.CreateComponentGroup(typeof(int));

            var fixedArray = group.GetComponentDataFixedArray<int>();

	        Assert.AreEqual(2, fixedArray.Length);
	        Assert.AreEqual(64, fixedArray[0].Length);
	        Assert.AreEqual(10, fixedArray[1].Length);

	        Assert.AreEqual(0, fixedArray[0][3]);
	        Assert.AreEqual(0, fixedArray[1][3]);
	        
			NativeArray<int > array;
		        
	        array = fixedArray[0];
	        array[3] = 0;

	        array = fixedArray[1];
	        array[3] = 1;

            for (int i = 0; i < fixedArray.Length; i++)
            {
                Assert.AreEqual(i, fixedArray[i][3]);
            }
        }
	}
}