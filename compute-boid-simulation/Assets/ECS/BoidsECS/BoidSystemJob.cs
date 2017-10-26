using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

namespace BoidSimulations
{
    /*
    struct BoidSystemJob : IJobProcessComponentData<BoidData>, IAutoComponentSystemJob
    {
        float dt;
        public void Prepare()
        {
            dt = Time.deltaTime;
        }

        public void Execute(ref BoidData boid)
        {
            boid.position.y -= 30 * dt;
        }
    }
    */
}