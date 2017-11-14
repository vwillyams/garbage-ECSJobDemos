using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

namespace UnityEngine.ECS
{
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class InjectComponentGroup : System.Attribute
	{

	}

	//@TODO: Remove
	[AttributeUsage(AttributeTargets.Field)]
    public class InjectTuples : System.Attribute
    {
    	public int TupleSetIndex { get; set; }

    	public InjectTuples(int tupleSetIndex = 0)
    	{
    		TupleSetIndex = tupleSetIndex;
    	}
    }
}