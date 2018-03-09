using Unity.Collections;
using UnityEngine.Experimental.AI;

public struct PolygonPathEcs
{
    public NativeArray<PolygonId> polygons;
    public NavMeshLocation start;
    public NavMeshLocation end;
    public int size;
}
