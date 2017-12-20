using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ECS
{
    [Serializable]
    public class SystemGraphState
    {
        public List<SystemViewData> systemViews = new List<SystemViewData>();
    }
}
