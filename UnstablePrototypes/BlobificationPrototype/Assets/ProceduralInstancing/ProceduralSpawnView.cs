﻿using UnityEngine;
using UnityEngine.ECS;

public class ProceduralSpawnView : MonoBehaviour
{
    public float Distance;

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawSphere(transform.position, Distance);
    }
}