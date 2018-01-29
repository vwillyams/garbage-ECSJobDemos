﻿using Data;
using UnityEngine;
using UnityEngine.ECS;

namespace Systems
{
    public class RotationSystem : ComponentSystem
    {
        struct Planets
        {
            public int Length;
            public ComponentDataArray<RotationData> Data;
            public ComponentArray<Transform> Transform;
        }

        [InjectComponentGroup] private Planets _planets;
        protected override void OnUpdate()
        {
            for (var i = 0; i < _planets.Length; ++i)
            {
                var transform = _planets.Transform[i];
                var rotSpeed = _planets.Data[i].RotationSpeed;
                transform.Rotate(rotSpeed);
            }
        }
    }
}