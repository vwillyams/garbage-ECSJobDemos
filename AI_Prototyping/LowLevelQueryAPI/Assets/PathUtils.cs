using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Collections;
using UnityEngine.Experimental.AI;

public class PathUtils
{
    public static float Perp2D(Vector3 u, Vector3 v)
    {
        return u.z * v.x - u.x * v.z;
    }

    public static void Swap(ref Vector3 a, ref Vector3 b)
    {
        var temp = a;
        a = b;
        b = temp;
    }

    // Retrace portals between corners register if type of polygon changes
    public static int RetracePortals(int startIndex, int endIndex
        , NativeArray<PolygonID> path, int n, Vector3 termPos
        , ref NativeArray<NavMeshLocation> straightPath
        , ref NativeArray<NavMeshStraightPathFlags> straightPathFlags
        , int maxStraightPath)
    {
        Assert.IsTrue(n < maxStraightPath);
        Assert.IsTrue(startIndex <= endIndex);

        for (int k = startIndex; k < endIndex - 1; ++k)
        {
            var type1 = NavMeshQuery.GetPolygonType(path[k]);
            var type2 = NavMeshQuery.GetPolygonType(path[k + 1]);
            if (type1 != type2)
            {
                Vector3 l, r;
                var status = NavMeshQuery.GetPortalPoints(path[k], path[k + 1], out l, out r);
                Assert.IsTrue(status); // Expect path elements k, k+1 to be verified

                Vector3 cpa1, cpa2;
                GeometryUtils.SegmentSegmentCPA(out cpa1, out cpa2, l, r, straightPath[n - 1].position, termPos);
                straightPath[n] = new NavMeshLocation() { polygon = path[k + 1].polygon, position = cpa1 };
                // TODO maybe the flag should be additive with |=
                straightPathFlags[n] = (type2 == NavMeshPolyTypes.kPolyTypeOffMeshConnection) ? NavMeshStraightPathFlags.kStraightPathOffMeshConnection : 0;
                if (++n == maxStraightPath)
                {
                    return maxStraightPath;
                }
            }
        }
        straightPath[n] = new NavMeshLocation() { polygon = path[endIndex].polygon, position = termPos };
        straightPathFlags[n] = NavMeshQuery.GetPolygonType(path[endIndex]) == NavMeshPolyTypes.kPolyTypeOffMeshConnection ? NavMeshStraightPathFlags.kStraightPathOffMeshConnection : 0;
        return ++n;
    }

    public static PathQueryStatus FindStraightPath(
        PolygonPath path
        , ref NativeArray<NavMeshLocation> straightPath
        , ref NativeArray<NavMeshStraightPathFlags> straightPathFlags
        , ref int straightPathCount
        , int maxStraightPath
        )
    {
        return FindStraightPath(path.start.position, path.end.position, path.polygons, path.size, ref straightPath, ref straightPathFlags, ref straightPathCount, maxStraightPath);
    }

    public static PathQueryStatus FindStraightPath(Vector3 startPos, Vector3 endPos
        , NativeArray<PolygonID> path, int pathSize
        , ref NativeArray<NavMeshLocation> straightPath
        , ref NativeArray<NavMeshStraightPathFlags> straightPathFlags
        , ref int straightPathCount
        , int maxStraightPath)
    {
        Assert.IsTrue(pathSize > 0, "FindStraightPath: The path cannot be empty");
        Assert.IsTrue(path.Length >= pathSize, "FindStraightPath: The array of path polygons must fit at least the size specified");
        Assert.IsTrue(maxStraightPath > 1, "FindStraightPath: At least two corners need to be returned, the start and end");
        Assert.IsTrue(straightPath.Length >= maxStraightPath, "FindStraightPath: The array of returned corners cannot be smaller than the desired maximum corner count");
        Assert.IsTrue(straightPathFlags.Length >= straightPath.Length, "FindStraightPath: The array of returned flags must not be smaller than the array of returned corners");

        // TODO Assert.IsTrue(startPos is in the polygon of path[0].polygonId);

        // TODO make PolygonID.valid to be ThreadSafe and use if (!path[0].valid)
        if (path[0].polygon == 0)
        {
            straightPath[0] = new NavMeshLocation();    // empty terminator
            return PathQueryStatus.Failure; // | kNavMeshInvalidParam;
        }

        straightPath[0] = new NavMeshLocation
        {
            position = startPos, // TODO make sure the start position is in this polygon?
            polygon = path[0].polygon   // TODO search the polygon on the path where the start position is
        };

        straightPathFlags[0] = NavMeshStraightPathFlags.kStraightPathStart;

        var apexIndex = 0;
        var n = 1;

        if (pathSize > 1)
        {
            var startPolyWorldToLocal = NavMeshQuery.PolygonWorldToLocalMatrix(path[0]);

            var apex = startPolyWorldToLocal.MultiplyPoint(startPos);
            var left = Vector3.zero;
            var right = Vector3.zero;
            var leftIndex = -1;
            var rightIndex = -1;

            for (var i = 1; i <= pathSize; ++i)
            {
                var polyWorldToLocal = NavMeshQuery.PolygonWorldToLocalMatrix(path[apexIndex]);

                Vector3 vl, vr;
                if (i == pathSize)
                {
                    vl = vr = polyWorldToLocal.MultiplyPoint(endPos);
                }
                else
                {
                    var success = NavMeshQuery.GetPortalPoints(path[i - 1], path[i], out vl, out vr);
                    if (!success)
                    {
                        return PathQueryStatus.Failure; // | kNavMeshInvalidParam;
                    }

                    //- TODO make PolygonID.valid to be ThreadSafe
                    Assert.IsTrue(path[i - 1].valid);
                    //- Assert.IsTrue(path[i].valid);
                    Assert.IsTrue(path[i - 1].polygon != 0);
                    Assert.IsTrue(path[i].polygon != 0);

                    vl = polyWorldToLocal.MultiplyPoint(vl);
                    vr = polyWorldToLocal.MultiplyPoint(vr);
                }

                vl = vl - apex;
                vr = vr - apex;

                // Ensure left/right ordering
                if (Perp2D(vl, vr) < 0)
                    Swap(ref vl, ref vr);

                // Terminate funnel by turning
                if (Perp2D(left, vr) < 0)
                {
                    var polyLocalToWorld = NavMeshQuery.PolygonLocalToWorldMatrix(path[apexIndex]);
                    var termPos = polyLocalToWorld.MultiplyPoint(apex + left);

                    n = RetracePortals(apexIndex, leftIndex, path, n, termPos, ref straightPath, ref straightPathFlags, maxStraightPath);
                    if (n == maxStraightPath)
                    {
                        straightPathCount = n;
                        return /*kNavMeshBufferTooSmall | */ PathQueryStatus.Success;
                    }

                    apex = polyWorldToLocal.MultiplyPoint(termPos);
                    left.Set(0, 0, 0);
                    right.Set(0, 0, 0);
                    i = apexIndex = leftIndex;
                    continue;
                }
                if (Perp2D(right, vl) > 0)
                {
                    var polyLocalToWorld = NavMeshQuery.PolygonLocalToWorldMatrix(path[apexIndex]);
                    var termPos = polyLocalToWorld.MultiplyPoint(apex + right);
                    n = RetracePortals(apexIndex, rightIndex, path, n, termPos, ref straightPath, ref straightPathFlags, maxStraightPath);
                    if (n == maxStraightPath)
                    {
                        straightPathCount = n;
                        return /*kNavMeshBufferTooSmall | */ PathQueryStatus.Success;
                    }

                    apex = polyWorldToLocal.MultiplyPoint(termPos);
                    left.Set(0, 0, 0);
                    right.Set(0, 0, 0);
                    i = apexIndex = rightIndex;
                    continue;
                }

                // Consider: additional termination test - based on changing up-vector in frame of reference

                // Narrow funnel
                if (Perp2D(left, vl) >= 0)
                {
                    left = vl;
                    leftIndex = i;
                }
                if (Perp2D(right, vr) <= 0)
                {
                    right = vr;
                    rightIndex = i;
                }
            }
        }

        // Remove the the next to last if duplicate point - e.g. start and end positions are the same
        // (in which case we have get a single point)
        if (n > 0 && (straightPath[n - 1].position == endPos))
            n--;

        n = RetracePortals(apexIndex, pathSize - 1, path, n, endPos, ref straightPath, ref straightPathFlags, maxStraightPath);
        if (n == maxStraightPath)
        {
            straightPathCount = n;
            return /*kNavMeshBufferTooSmall | */ PathQueryStatus.Success;
        }

        // Fix flag for final path point
        straightPathFlags[n - 1] = NavMeshStraightPathFlags.kStraightPathEnd;

        straightPathCount = n;
        return PathQueryStatus.Success;
    }
}
