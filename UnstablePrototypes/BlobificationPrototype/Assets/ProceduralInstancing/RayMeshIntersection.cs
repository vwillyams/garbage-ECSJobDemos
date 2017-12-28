using Unity.Mathematics;
using static Unity.Mathematics.math;

public struct CollisionMeshData
{
	public BlobArray<float3> Vertices;
    public BlobArray<int3>   Triangles;
}

static public class GeometryUtility
{
    public static bool CircleIntersectsRectangle(float2 circleCenter, float circleRadius, float2 rectMin, float2 rectMax)
    {
        var delta = circleCenter - max(rectMin, min(circleCenter, rectMax));
        return dot(delta, delta) < (circleRadius * circleRadius);
    }

    static public bool RayIntersectsMesh(float3 rayOrigin, float3 rayVector, ref CollisionMeshData meshData, out float3 outIntersectionPoint)
    {
        for (int i = 0;i != meshData.Triangles.Length;i++)
        {
            //@TODO: Find closest triangle...
            int3 tri = meshData.Triangles[i];

            if (RayIntersectsTriangle(
                rayOrigin, rayVector,
                meshData.Vertices[tri.x], meshData.Vertices[tri.y], meshData.Vertices[tri.z],
                out outIntersectionPoint))
            {
                return true;
            }
        }

        outIntersectionPoint = new float3(0);
        return false;
    }

    static public bool RayIntersectsTriangle(
		float3 rayOrigin, float3 rayVector,
		float3 vertex0, float3 vertex1, float3 vertex2,
	    out float3 outIntersectionPoint)
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
	        outIntersectionPoint = new float3(0);
	        return false;
	    }

	    f = 1 / a;
	    s = rayOrigin - vertex0;
	    u = f * (dot(s, h));
	    if (u < 0.0 || u > 1.0)
	    {
	        outIntersectionPoint = new float3(0);
	        return false;
	    }

	    q = cross(s, edge1);
	    v = f * dot(rayVector, q);
	    if (v < 0.0 || u + v > 1.0)
	    {
	        outIntersectionPoint = new float3(0);
	        return false;
	    }

	    // At this stage we can compute t to find out where the intersection point is on the line.
	    float t = f * dot (edge2, q);
	    if (t > EPSILON) // ray intersection
	    {
	        outIntersectionPoint = rayOrigin + rayVector * t;
	        return true;
	    }
	    else
	    {
	    	// This means that there is a line intersection but not a ray intersection.
	        outIntersectionPoint = new float3(0);
	    	return false;
	    }

	}
}
