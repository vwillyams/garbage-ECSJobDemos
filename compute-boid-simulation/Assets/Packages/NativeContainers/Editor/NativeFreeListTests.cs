using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine.Jobs;
using UnityEngine.Collections;
#pragma warning disable 0219

public class NativeFreeListTests
{
	[Test]
	public void AddRemove()
	{
		var list = new NativeFreeList<int>(Allocator.TempJob);
		Assert.AreEqual(0, list.Add (0));
		Assert.AreEqual(1, list.Add (1));
		Assert.AreEqual(2, list.Add (2));
		list.Remove (1);
		Assert.AreEqual (2, list.Length);

		Assert.AreEqual (0, list[0]);
		Assert.AreEqual (2, list[2]);

		Assert.AreEqual(1, list.Add (3));

		list.Dispose();
	}

	[Test]
	public void AutoCapacity()
	{
		var list = new NativeFreeList<int>(Allocator.TempJob);
		for (int i = 0;i!=1000;i++)
			Assert.AreEqual(i, list.Add (i));

		for (int i = 0;i!=1000;i++)
			list[i] = i * 2;
		for (int i = 0;i!=1000;i++)
			Assert.AreEqual(i * 2, list[i]);

		for (int i = 0;i!=1000;i++)
			list.Remove (i);

		list.Dispose();
	}

}


