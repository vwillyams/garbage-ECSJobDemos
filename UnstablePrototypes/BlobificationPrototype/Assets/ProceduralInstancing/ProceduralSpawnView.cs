using UnityEngine;
using Unity.ECS;

public class ProceduralSpawnView : MonoBehaviour
{
    public float Distance;

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawSphere(transform.position, Distance);
    }
}
