using UnityEngine;
using Unity.Entities;

public class ProceduralSpawnView : MonoBehaviour
{
    public float Distance;

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawSphere(transform.position, Distance);
    }
}
