using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ECS
{

    [Serializable]
    public class SystemGraphEdge
    {
        public List<Vector3> points = new List<Vector3>();
        public int target;
    }
    
}