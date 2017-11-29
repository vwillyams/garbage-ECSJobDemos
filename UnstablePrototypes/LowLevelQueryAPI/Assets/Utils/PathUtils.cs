using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.AI;

[Flags]
public enum StraightPathFlags
{
    Start = 0x01,              // The vertex is the start position.
    End = 0x02,                // The vertex is the end position.
    OffMeshConnection = 0x04   // The vertex is start of an off-mesh link.
}

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
    public static int RetracePortals(NavMeshQuery query, int startIndex, int endIndex
        , NativeSlice<PolygonID> path, int n, Vector3 termPos
        , ref NativeArray<NavMeshLocation> straightPath
        , ref NativeArray<StraightPathFlags> straightPathFlags
        , int maxStraightPath)
    {
#if DEBUG_CROWDSYSTEM_ASSERTS
        Assert.IsTrue(n < maxStraightPath);
        Assert.IsTrue(startIndex <= endIndex);
#endif

        for (int k = startIndex; k < endIndex - 1; ++k)
        {
            var type1 = query.GetPolygonType(path[k]);
            var type2 = query.GetPolygonType(path[k + 1]);
            if (type1 != type2)
            {
                Vector3 l, r;
                var status = query.GetPortalPoints(path[k], path[k + 1], out l, out r);

#if DEBUG_CROWDSYSTEM_ASSERTS
                Assert.IsTrue(status); // Expect path elements k, k+1 to be verified
#endif

                float3 cpa1, cpa2;
                GeometryUtils.SegmentSegmentCPA(out cpa1, out cpa2, l, r, straightPath[n - 1].position, termPos);
                straightPath[n] = new NavMeshLocation(cpa1, path[k + 1]);

                // TODO maybe the flag should be additive with |=
                straightPathFlags[n] = (type2 == NavMeshPolyTypes.OffMeshConnection) ? StraightPathFlags.OffMeshConnection : 0;
                if (++n == maxStraightPath)
                {
                    return maxStraightPath;
                }
            }
        }
        straightPath[n] = new NavMeshLocation(termPos, path[endIndex]);
        straightPathFlags[n] = query.GetPolygonType(path[endIndex]) == NavMeshPolyTypes.OffMeshConnection ? StraightPathFlags.OffMeshConnection : 0;
        return ++n;
    }

    public static PathQueryStatus FindStraightPath(
        NavMeshQuery query, PolygonPathEcs path
        , ref NativeArray<NavMeshLocation> straightPath
        , ref NativeArray<StraightPathFlags> straightPathFlags
        , ref NativeArray<float> vertexSide
        , ref int straightPathCount
        , int maxStraightPath
    )
    {
        return FindStraightPath(query, path.start.position, path.end.position, new NativeSlice<PolygonID>(path.polygons), path.size, ref straightPath, ref straightPathFlags, ref vertexSide, ref straightPathCount, maxStraightPath);
    }

    public static PathQueryStatus FindStraightPath(NavMeshQuery query, Vector3 startPos, Vector3 endPos
        , NativeSlice<PolygonID> path, int pathSize
        , ref NativeArray<NavMeshLocation> straightPath
        , ref NativeArray<StraightPathFlags> straightPathFlags
        , ref NativeArray<float> vertexSide
        , ref int straightPathCount
        , int maxStraightPath)
    {
#if DEBUG_CROWDSYSTEM_ASSERTS
        Assert.IsTrue(pathSize > 0, "FindStraightPath: The path cannot be empty");
        Assert.IsTrue(path.Length >= pathSize, "FindStraightPath: The array of path polygons must fit at least the size specified");
        Assert.IsTrue(maxStraightPath > 1, "FindStraightPath: At least two corners need to be returned, the start and end");
        Assert.IsTrue(straightPath.Length >= maxStraightPath, "FindStraightPath: The array of returned corners cannot be smaller than the desired maximum corner count");
        Assert.IsTrue(straightPathFlags.Length >= straightPath.Length, "FindStraightPath: The array of returned flags must not be smaller than the array of returned corners");
#endif

        // TODO // Assert.IsTrue(startPos is in the polygon of path[0].polygonId);

        if (!query.IsValid(path[0]))
        {
            straightPath[0] = new NavMeshLocation(); // empty terminator
            return PathQueryStatus.Failure; // | kNavMeshInvalidParam;
        }

        straightPath[0] = new NavMeshLocation(
            startPos, // TODO make sure the start position is in this polygon?
            path[0] // TODO search the polygon on the path where the start position is
        );

        straightPathFlags[0] = StraightPathFlags.Start;

        var apexIndex = 0;
        var n = 1;

        if (pathSize > 1)
        {
            var startPolyWorldToLocal = query.PolygonWorldToLocalMatrix(path[0]);

            var apex = startPolyWorldToLocal.MultiplyPoint(startPos);
            var left = Vector3.zero;
            var right = Vector3.zero;
            var leftIndex = -1;
            var rightIndex = -1;

            for (var i = 1; i <= pathSize; ++i)
            {
                var polyWorldToLocal = query.PolygonWorldToLocalMatrix(path[apexIndex]);

                Vector3 vl, vr;
                if (i == pathSize)
                {
                    vl = vr = polyWorldToLocal.MultiplyPoint(endPos);
                }
                else
                {
                    var success = query.GetPortalPoints(path[i - 1], path[i], out vl, out vr);
                    if (!success)
                    {
                        return PathQueryStatus.Failure; // | kNavMeshInvalidParam;
                    }

#if DEBUG_CROWDSYSTEM_ASSERTS
                    //- TODO make PolygonID.valid to be ThreadSafe
                    Assert.IsTrue(path[i - 1].valid);
                    //- Assert.IsTrue(path[i].valid);
                    Assert.IsTrue(path[i - 1].polygon != 0);
                    Assert.IsTrue(path[i].polygon != 0);
#endif

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
                    var polyLocalToWorld = query.PolygonLocalToWorldMatrix(path[apexIndex]);
                    var termPos = polyLocalToWorld.MultiplyPoint(apex + left);

                    n = RetracePortals(query, apexIndex, leftIndex, path, n, termPos, ref straightPath, ref straightPathFlags, maxStraightPath);
                    if (vertexSide.Length > 0)
                    {
                        vertexSide[n - 1] = -1;
                    }

                    //Debug.Log("LEFT");

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
                    var polyLocalToWorld = query.PolygonLocalToWorldMatrix(path[apexIndex]);
                    var termPos = polyLocalToWorld.MultiplyPoint(apex + right);

                    n = RetracePortals(query, apexIndex, rightIndex, path, n, termPos, ref straightPath, ref straightPathFlags, maxStraightPath);
                    if (vertexSide.Length > 0)
                    {
                        vertexSide[n - 1] = 1;
                    }

                    //Debug.Log("RIGHT");

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

        n = RetracePortals(query, apexIndex, pathSize - 1, path, n, endPos, ref straightPath, ref straightPathFlags, maxStraightPath);
        if (vertexSide.Length > 0)
        {
            vertexSide[n - 1] = 0;
        }

        if (n == maxStraightPath)
        {
            straightPathCount = n;
            return /*kNavMeshBufferTooSmall | */ PathQueryStatus.Success;
        }

        // Fix flag for final path point
        straightPathFlags[n - 1] = StraightPathFlags.End;

        straightPathCount = n;
        return PathQueryStatus.Success;
    }
}
