using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;

[UpdateBefore(typeof(LineRenderSystem))]
public class AsteroidRenderSystem : ComponentSystem
{
    [Inject]
    LineRenderSystem m_LineRenderSystem;

    struct Asteroid
    {
        public int Length;
        public ComponentDataArray<AsteroidTagComponentData> tag;
        public ComponentArray<Transform> transform;
    }

    [InjectComponentGroup]
    Asteroid asteroids;

    static float pulse = 1;
    static float pulseDelta = 1;
    static float pulseMax = 1.2f;
    static float pulseMin = 0.8f;

    override protected void OnUpdate()
    {
        NativeList<LineRenderSystem.Line> lines = m_LineRenderSystem.LineList;
        float astrWidth = 30;
        float astrHeight = 30;
        float astrLineWidth = 2;
        float4 astrColor = new float4(0.25f, 0.85f, 0.85f, 1);
        float2 astrTL = new float2(-astrWidth/2,-astrHeight/2);
        float2 astrTR = new float2(astrWidth/2,-astrHeight/2);
        float2 astrBL = new float2(-astrWidth/2,astrHeight/2);
        float2 astrBR = new float2(astrWidth/2,astrHeight/2);

        pulse += pulseDelta / 60;
        if (pulse > pulseMax)
        {
            pulse = pulseMax;
            pulseDelta = -pulseDelta;
        }
        else if (pulse < pulseMin)
        {
            pulse = pulseMin;
            pulseDelta = -pulseDelta;
        }

        for (int asteroid = 0; asteroid < asteroids.Length; ++asteroid)
        {
            float2 pos = LineRenderSystem.screenPosFromTransform(asteroids.transform[asteroid].position);
            var rot = asteroids.transform[asteroid].rotation;
            var rotTL = pos+LineRenderSystem.rotatePos(astrTL, rot)*pulse;
            var rotTR = pos+LineRenderSystem.rotatePos(astrTR, rot)*pulse;
            var rotBL = pos+LineRenderSystem.rotatePos(astrBL, rot)*pulse;
            var rotBR = pos+LineRenderSystem.rotatePos(astrBR, rot)*pulse;
            lines.Add(new LineRenderSystem.Line(rotTL, rotTR, astrColor, astrLineWidth));
            lines.Add(new LineRenderSystem.Line(rotTL, rotBL, astrColor, astrLineWidth));
            lines.Add(new LineRenderSystem.Line(rotTR, rotBR, astrColor, astrLineWidth));
            lines.Add(new LineRenderSystem.Line(rotBL, rotBR, astrColor, astrLineWidth));
        }
    }
}