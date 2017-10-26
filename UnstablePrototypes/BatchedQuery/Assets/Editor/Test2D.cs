using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using UnityEngine.Collections;

public class Test2D
{
    private const float epsilon = 0.00001f;

    [Test]
    public void TestRaycastSingle2D()
    {
        // spawn a sphere collider at origin
        var g = new GameObject();
        g.AddComponent<CircleCollider2D>();

        var results = new NativeArray<RaycastHit2D>(1, Allocator.Persistent);

        var commands = new NativeArray<RaycastCommand2D>(1, Allocator.Persistent);

        Vector3 origin = Vector2.left * -10f;
        Vector3 direction = Vector3.right;

        commands[0] = new RaycastCommand2D(origin, direction, 100.0f);

        RaycastCommand2D.ScheduleBatch(commands, results, 10).Complete();

        // now compare the result to the synchronous version
        RaycastHit2D hit = Physics2D.Raycast(origin, direction);

        RaycastHit2D batchedHit = results[0];

        results.Dispose();
        commands.Dispose();

        Assert.AreEqual(hit.point.x, batchedHit.point.x, epsilon);
        Assert.AreEqual(hit.point.y, batchedHit.point.y, epsilon);
        Assert.AreEqual(hit.collider, batchedHit.collider);
    }
}
