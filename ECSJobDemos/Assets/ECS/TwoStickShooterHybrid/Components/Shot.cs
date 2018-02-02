using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickHybridExample
{

    public class Shot : MonoBehaviour
    {
        [HideInInspector] public float Speed;
        [HideInInspector] public float TimeToLive;
    }
}
