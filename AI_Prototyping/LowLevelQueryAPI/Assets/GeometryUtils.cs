using UnityEngine;

public class GeometryUtils
{
    // Calculate the closest point of approach for line-segment vs line-segment.
    public static bool SegmentSegmentCPA(out Vector3 c0, out Vector3 c1, Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1)
    {
        Vector3 u = p1 - p0;
        Vector3 v = q1 - q0;
        Vector3 w0 = p0 - q0;

        float a = Vector3.Dot(u, u);
        float b = Vector3.Dot(u, v);
        float c = Vector3.Dot(v, v);
        float d = Vector3.Dot(u, w0);
        float e = Vector3.Dot(v, w0);

        float den = (a * c - b * b);
        float sc, tc;

        if (den == 0)
        {
            sc = 0;
            tc = d / b;
            // todo: handle b = 0 (=> a and/or c is 0)
        }
        else
        {
            sc = (b * e - c * d) / (a * c - b * b);
            tc = (a * e - b * d) / (a * c - b * b);
        }

        c0 = Vector3.Lerp(p0, p1, sc);
        c1 = Vector3.Lerp(q0, q1, tc);

        return den != 0;
    }
}
