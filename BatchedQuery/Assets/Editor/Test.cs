using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using UnityEngine.Collections;

public class Test
{
    private const float epsilon = 0.00001f;

    GameObject sphereGo;
    GameObject cubeGo;

    const string LAYER_NAME = "Water";

    [TestFixtureSetUp]
    public void SetUp()
    {
        sphereGo = new GameObject();
        sphereGo.AddComponent<SphereCollider>();
        sphereGo.layer = LayerMask.NameToLayer(LAYER_NAME);

        cubeGo = new GameObject();
        cubeGo.transform.position = new Vector3(0, 0, 5);
        cubeGo.AddComponent<BoxCollider>();
    }

    [Test]
    public void TestRaycastSingle()
    {
        var results = new NativeArray<RaycastHit>(1, Allocator.Persistent);
        var commands = new NativeArray<RaycastCommand>(1, Allocator.Persistent);

        Vector3 origin = Vector3.forward * -10;
        Vector3 direction = Vector3.forward;

        commands[0] = new RaycastCommand(origin, direction);

        RaycastCommand.ScheduleBatch(commands, results, 10).Complete();

        // now compare the result to the synchronous version
        RaycastHit hit = new RaycastHit();
        bool found = Physics.Raycast(origin, direction, out hit);

        RaycastHit batchedHit = results[0];

        results.Dispose();
        commands.Dispose();

        Assert.AreEqual(hit.point.x, batchedHit.point.x, epsilon);
        Assert.AreEqual(hit.point.y, batchedHit.point.y, epsilon);
        Assert.AreEqual(hit.point.z, batchedHit.point.z, epsilon);
        Assert.AreEqual(hit.collider, batchedHit.collider);
    }

    [Test]
    public void TestRaycastWithMask()
    {
        var results = new NativeArray<RaycastHit>(1, Allocator.Persistent);
        var commands = new NativeArray<RaycastCommand>(1, Allocator.Persistent);

        Vector3 origin = Vector3.forward * -10;
        Vector3 direction = Vector3.forward;

        // raycast against anything not in the Water layer
        commands[0] = new RaycastCommand(origin, direction, float.MaxValue, ~LayerMask.GetMask(LAYER_NAME));

        RaycastCommand.ScheduleBatch(commands, results, 10).Complete();

        RaycastHit batchedHit = results[0];

        results.Dispose();
        commands.Dispose();

        Assert.AreEqual(cubeGo.GetComponent<BoxCollider>(), batchedHit.collider, "Hit the wrong collider");
    }

    [Test]
    public void TestSpherecastWithMask()
    {
        var results = new NativeArray<RaycastHit>(1, Allocator.Persistent);
        var commands = new NativeArray<SpherecastCommand>(1, Allocator.Persistent);

        Vector3 origin = Vector3.forward * -10;
        Vector3 direction = Vector3.forward;

        // raycast against anything not in the Water layer
        commands[0] = new SpherecastCommand(origin, 1f,  direction, float.MaxValue, ~LayerMask.GetMask(LAYER_NAME));

        SpherecastCommand.ScheduleBatch(commands, results, 10).Complete();

        RaycastHit batchedHit = results[0];

        results.Dispose();
        commands.Dispose();

        Assert.NotNull(batchedHit.collider);

        Assert.AreEqual(cubeGo.GetComponent<BoxCollider>(), batchedHit.collider, "Hit the wrong collider");
    }

    [Test]
    public void TestSphereOverlapWithMask()
    {
        var results = new NativeArray<RaycastHit>(1, Allocator.Persistent);
        var commands = new NativeArray<SphereOverlapCommand>(1, Allocator.Persistent);

        // Check against anything not in the Water layer
        commands[0] = new SphereOverlapCommand(new Vector3(0, 0, -10f), 15f, ~LayerMask.GetMask(LAYER_NAME));

        SphereOverlapCommand.ScheduleBatch(commands, results, 10).Complete();

        RaycastHit batchedHit = results[0];

        results.Dispose();
        commands.Dispose();

        Assert.NotNull(batchedHit.collider);

        Assert.AreEqual(cubeGo.GetComponent<BoxCollider>(), batchedHit.collider, "Hit the wrong collider");
        Assert.AreEqual(new Vector3(0f, 0f, 4.5f), batchedHit.point, "Incorrect hit point.");
        Assert.AreEqual(new Vector3(0f, 0f, 1f), batchedHit.normal, "Incorrect hit normal.");
        Assert.AreEqual(0.5f, batchedHit.distance, "Incorrect hit distance.");
    }
}
