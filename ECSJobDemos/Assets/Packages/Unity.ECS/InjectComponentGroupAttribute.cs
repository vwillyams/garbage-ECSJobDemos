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
	public sealed class InjectComponentGroupAttribute : System.Attribute
	{

	}
}