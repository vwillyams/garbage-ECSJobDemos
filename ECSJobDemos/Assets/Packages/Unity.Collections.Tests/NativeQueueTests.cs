using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections;

public class NativeQueueTests
{
	[Test]
	public void Enqueue_Dequeue()
	{
		var queue = new NativeQueue<int> (16, Allocator.Temp);
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		for (int i = 0; i < 16; ++i)
			queue.Enqueue(i);
		Assert.AreEqual(16, queue.Count);
		for (int i = 0; i < 16; ++i)
			Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		queue.Dispose ();
	}
	[Test]
	public void ConcurrentEnqueue_Dequeue()
	{
		var queue = new NativeQueue<int> (16, Allocator.Temp);
		NativeQueue<int>.Concurrent cQueue = queue;
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		for (int i = 0; i < 16; ++i)
			cQueue.Enqueue(i);
		Assert.AreEqual(16, queue.Count);
		for (int i = 0; i < 16; ++i)
			Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		queue.Dispose ();
	}

	[Test]
	public void Enqueue_Dequeue_Peek()
	{
		var queue = new NativeQueue<int> (16, Allocator.Temp);
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		for (int i = 0; i < 16; ++i)
			queue.Enqueue(i);
		Assert.AreEqual(16, queue.Count);
		for (int i = 0; i < 16; ++i)
		{
			Assert.AreEqual(i, queue.Peek(), "Got the wrong value from the queue");
			queue.Dequeue();
		}
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		queue.Dispose ();
	}

	[Test]
	public void Enqueue_Dequeue_Clear()
	{
		var queue = new NativeQueue<int> (16, Allocator.Temp);
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		for (int i = 0; i < 16; ++i)
			queue.Enqueue(i);
		Assert.AreEqual(16, queue.Count);
		for (int i = 0; i < 8; ++i)
			Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
		Assert.AreEqual(8, queue.Count);
		queue.Clear();
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		queue.Dispose ();
	}

	[Test]
	public void Full_Queue_Throws()
	{
		var queue = new NativeQueue<int> (16, Allocator.Temp);
		// Fill the queue
		for (int i = 0; i < 16; ++i)
			queue.Enqueue(i);
		// Make sure overallocating throws and exception if using the Concurrent version - normal queue would grow
		NativeQueue<int>.Concurrent cQueue = queue;
		Assert.Throws<System.InvalidOperationException> (()=> {cQueue.Enqueue(100); });
		queue.Dispose ();
	}

	[Test]
	public void Double_Deallocate_Throws()
	{
		var queue = new NativeQueue<int> (16, Allocator.Temp);
		queue.Dispose ();
		Assert.Throws<System.InvalidOperationException> (() => { queue.Dispose (); });
	}

	[Test]
	public void QueueSupportsAutomaticCapacityChange()
	{
		var queue = new NativeQueue<int> (1, Allocator.Temp);
		// Make sure inserting values work and grows the capacity
		for (int i = 0; i < 8; ++i)
			queue.Enqueue(i);
		Assert.IsTrue(queue.Capacity >= 8, "Capacity was not updated correctly");
		Assert.AreEqual (8, queue.Count);
		// Make sure reading the inserted values work
		for (int i = 0; i < 8; ++i)
		{
			Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
		}
		queue.Dispose ();
	}

	[Test]
	public void QueueEmptyCapacity()
	{
		var queue = new NativeQueue<int> (0, Allocator.Persistent);
		queue.Enqueue (0);
		Assert.AreEqual (1, queue.Capacity);
		Assert.AreEqual (1, queue.Count);
		queue.Dispose ();
	}

	[Test]
	public void EnqueueScalability()
	{
		var queue = new NativeQueue<int> (1, Allocator.Persistent);
		for (int i = 0; i != 1000 * 100; i++)
		{
			queue.Enqueue (i);
		}

		Assert.AreEqual (1000 * 100, queue.Count);

		for (int i = 0; i != 1000 * 100; i++)
			Assert.AreEqual (i, queue.Dequeue());
		Assert.AreEqual (0, queue.Count);

		queue.Dispose ();
	}

	[Test]
	public void Enqueue_Wrap()
	{
		var queue = new NativeQueue<int> (256, Allocator.Temp);
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		for (int i = 0; i < 256; ++i)
			queue.Enqueue(i);
		Assert.AreEqual(256, queue.Count);
		for (int i = 0; i < 128; ++i)
			Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
		Assert.AreEqual(128, queue.Count);
		for (int i = 0; i < 128; ++i)
			queue.Enqueue(i);
		Assert.AreEqual(256, queue.Count);
		for (int i = 128; i < 256; ++i)
			Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
		Assert.AreEqual(128, queue.Count);
		for (int i = 0; i < 128; ++i)
			Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		queue.Dispose ();
	}
	[Test]
	public void ConcurrentEnqueue_Wrap()
	{
		var queue = new NativeQueue<int> (256, Allocator.Temp);
		NativeQueue<int>.Concurrent cQueue = queue;
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		for (int i = 0; i < 256; ++i)
			cQueue.Enqueue(i);
		Assert.AreEqual(256, queue.Count);
		for (int i = 0; i < 128; ++i)
			Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
		Assert.AreEqual(128, queue.Count);
		for (int i = 0; i < 128; ++i)
			cQueue.Enqueue(i);
		Assert.AreEqual(256, queue.Count);
		for (int i = 128; i < 256; ++i)
			Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
		Assert.AreEqual(128, queue.Count);
		for (int i = 0; i < 128; ++i)
			Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		queue.Dispose ();
	}
	[Test]
	public void Enqueue_Wrap_AlmostFull()
	{
		var queue = new NativeQueue<int> (256, Allocator.Temp);
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		for (int i = 0; i < 256; ++i)
			queue.Enqueue(i);
		Assert.AreEqual(256, queue.Count);

		for (int i = 0; i < 1000 * 100; ++i)
		{
			Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
			queue.Enqueue(i + 256);
		}

		Assert.AreEqual(256, queue.Count);
		Assert.AreEqual(256, queue.Capacity);
		for (int i = 0; i < 256; ++i)
			queue.Dequeue();
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		queue.Dispose ();
	}
	[Test]
	public void ConcurrentEnqueue_Wrap_AlmostFull()
	{
		var queue = new NativeQueue<int> (256, Allocator.Temp);
		NativeQueue<int>.Concurrent cQueue = queue;
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		for (int i = 0; i < 256; ++i)
			cQueue.Enqueue(i);
		Assert.AreEqual(256, queue.Count);

		for (int i = 0; i < 1000 * 100; ++i)
		{
			Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
			cQueue.Enqueue(i + 256);
		}

		Assert.AreEqual(256, queue.Count);
		Assert.AreEqual(256, queue.Capacity);
		for (int i = 0; i < 256; ++i)
			queue.Dequeue();
		Assert.AreEqual(0, queue.Count);
		Assert.Throws<System.InvalidOperationException> (()=> {queue.Dequeue(); });
		queue.Dispose ();
	}
}
