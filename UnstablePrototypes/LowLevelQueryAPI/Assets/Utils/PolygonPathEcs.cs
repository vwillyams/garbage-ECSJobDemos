using Unity.Collections;
using UnityEngine.Experimental.AI;

public struct PolygonPathEcs
{
    public NativeArray<PolygonID> polygons;
    public NavMeshLocation start;
    public NavMeshLocation end;
    public int size;
}
