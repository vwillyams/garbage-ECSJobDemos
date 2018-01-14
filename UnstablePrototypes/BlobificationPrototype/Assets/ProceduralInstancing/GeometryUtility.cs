using Unity.Mathematics;
using UnityEngine.ECS;
using static Unity.Mathematics.math;

static public class GeometryUtility
{
    public static bool CircleIntersectsRectangle(float2 circleCenter, float circleRadius, float2 rectMin, float2 rectMax)
    {
        var delta = circleCenter - max(rectMin, min(circleCenter, rectMax));
        return dot(delta, delta) < (circleRadius * circleRadius);
    }

    //@TODO: ARG... Mathlib not helping me... Need float3x4 etc...
    static float3 mul(float4x4 transform, float3 pos)
    {
        return math.mul(transform, new float4(pos, 1)).xyz;
    }

    unsafe public static bool RayIntersectsWorld(float3 rayOrigin, float3 rayVector, ComponentDataArray<CollisionMeshInstance>  CollisionInstances, out float3 outIntersectionPoint)
    {
        float closestT = float.PositiveInfinity;

        for (int i = 0; i != CollisionInstances.Length; i++)
        {
            var collider = CollisionInstances[i];

            float outT0;
            float outT1;
            if (IntersectRayAABB(rayOrigin, rayVector, collider.Bounds.Center, collider.Bounds.Extents,
                out outT0, out outT1) && outT0 < closestT)
            {
                CollisionMeshData* mesh = (CollisionMeshData*)collider.CollisionMesh.GetUnsafePtr();

                float t;
                GeometryUtility.RayIntersectsMesh(rayOrigin, rayVector, collider.Transform, ref *mesh, out t);

                closestT = math.min(t, closestT);
            }
        }

        outIntersectionPoint = rayOrigin + rayVector * closestT;
        bool res = closestT != float.PositiveInfinity;
        return res;
    }

    static public bool RayIntersectsMesh(float3 rayOrigin, float3 rayVector, float4x4 transform, ref CollisionMeshData meshData, out float outT)
    {
        //@TODO: Find closest triangle...
        //@TODO: perform raycast in local space...

        outT = float.PositiveInfinity;

        for (int i = 0;i != meshData.Triangles.Length;i++)
        {
            int3 tri = meshData.Triangles[i];
            float t;
            RayIntersectsTriangle(
                rayOrigin, rayVector,
                mul(transform, meshData.Vertices[tri.x]),
                mul(transform, meshData.Vertices[tri.y]),
                mul(transform, meshData.Vertices[tri.z]),
                out t);
            outT = math.min(outT, t);
        }

        return outT != float.PositiveInfinity;
    }

    static public bool RayIntersectsTriangle(
		float3 rayOrigin, float3 rayVector,
		float3 vertex0, float3 vertex1, float3 vertex2,
        out float outT)
	{
	    const float EPSILON = 0.0000001F;

	    float3 edge1, edge2, h, s, q;
	    float a,f,u,v;
	    edge1 = vertex1 - vertex0;
	    edge2 = vertex2 - vertex0;
	    h = cross(rayVector, edge2);
	    a = dot(edge1, h);

	    if (a > -EPSILON && a < EPSILON)
	    {
	        outT = float.PositiveInfinity;
	        return false;
	    }

	    f = 1 / a;
	    s = rayOrigin - vertex0;
	    u = f * (dot(s, h));
	    if (u < 0.0 || u > 1.0)
	    {
	        outT = float.PositiveInfinity;
	        return false;
	    }

	    q = cross(s, edge1);
	    v = f * dot(rayVector, q);
	    if (v < 0.0 || u + v > 1.0)
	    {
	        outT = float.PositiveInfinity;
	        return false;
	    }

	    // At this stage we can compute t to find out where the intersection point is on the line.
	    float t = f * dot (edge2, q);
	    if (t > EPSILON) // ray intersection
	    {
	        outT = t;
	        return true;
	    }
	    else
	    {
	    	// This means that there is a line intersection but not a ray intersection.
	        outT = float.PositiveInfinity;
	    	return false;
	    }

	}

    public static bool IntersectRayAABB(float3 rayOrigin, float3 rayVector, float3 boundsCenter, float3 boundsExtents, out float outT0, out float outT1)
    {
        outT0 = float.PositiveInfinity;
        outT1 = float.PositiveInfinity;

        float tmin = float.NegativeInfinity;
        float tmax = float.PositiveInfinity;

        float t0, t1, f;

        float3 p = boundsCenter - rayVector;
        for (int i = 0; i < 3; i++)
        {
            // ray and plane are parallel so no valid intersection can be found
            if (rayVector[i] != 0.0F)
            {
                f = 1.0F / rayVector[i];
                t0 = (p[i] + boundsExtents[i]) * f;
                t1 = (p[i] - boundsExtents[i]) * f;
                // Ray leaves on Right, Top, Back Side
                if (t0 < t1)
                {
                    if (t0 > tmin)
                        tmin = t0;

                    if (t1 < tmax)
                        tmax = t1;

                    if (tmin > tmax)
                        return false;

                    if (tmax < 0.0F)
                        return false;
                }
                // Ray leaves on Left, Bottom, Front Side
                else
                {
                    if (t1 > tmin)
                        tmin = t1;

                    if (t0 < tmax)
                        tmax = t0;

                    if (tmin > tmax)
                        return false;

                    if (tmax < 0.0F)
                        return false;
                }
            }
        }

        outT0 = tmin;
        outT1 = tmax;

        return true;
    }

    public static MinMaxBounds CalculateBounds(float4x4 transform, ref CollisionMeshData meshData)
    {
        var bounds = MinMaxBounds.Empty;
        for (int i = 0; i != meshData.Vertices.Length;i++)
            bounds.Encapsulate(mul(transform, meshData.Vertices[i]));
        return bounds;
    }
}
