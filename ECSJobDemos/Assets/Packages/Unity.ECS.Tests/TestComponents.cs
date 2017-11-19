using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Tests
{
	public struct EcsTestData : IComponentData
	{
		public int value;

		public EcsTestData(int inValue) { value = inValue; }
	}

	public class EcsTestComponent : ComponentDataWrapper<EcsTestData> { }


    public struct EcsTestData2 : IComponentData
    {
        public int value0;
        public int value1;

        public EcsTestData2(int inValue) { value1 = value0 = inValue; }
    }

    public struct EcsTestData3 : IComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestData3(int inValue) { value2 = value1 = value0 = inValue; }
    }

}