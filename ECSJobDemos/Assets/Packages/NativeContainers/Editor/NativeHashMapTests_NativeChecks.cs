using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections;

#if ENABLE_NATIVE_ARRAY_CHECKS
public class NativeHashMapTests_NativeChecks
{
	[Test]
	public void Double_Deallocate_Throws()
	{
		var hashMap = new NativeMultiHashMap<int, int> (16, Allocator.Temp);
		hashMap.Dispose ();
		Assert.Throws<System.InvalidOperationException> (() => { hashMap.Dispose (); });
	}
}
#endif
