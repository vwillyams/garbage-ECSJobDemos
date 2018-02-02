using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickClassicExample
{
    public class Transform2D : MonoBehaviour
    {
        public float2 Position;
        public float2 Heading;

        private void LateUpdate()
        {
            transform.position = new float3(Position.x, 0, Position.y);
        }
    }
}
