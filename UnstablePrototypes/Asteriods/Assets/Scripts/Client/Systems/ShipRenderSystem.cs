using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;

[UpdateBefore(typeof(LineRenderSystem))]
public class ShipRenderSystem : ComponentSystem
{
    [Inject]
    LineRenderSystem m_LineRenderSystem;

    struct Spaceships
    {
        public int Length;
        public ComponentDataArray<PlayerTagComponentData> self;
        public ComponentDataArray<PlayerInputComponentData> input;
        public ComponentArray<Transform> transform;
    }

    [InjectComponentGroup]
    Spaceships spaceships;

    override protected void OnUpdate()
    {
        NativeList<LineRenderSystem.Line> lines = m_LineRenderSystem.LineList;
        float shipWidth = 10;
        float shipHeight = 20;
        float shipLineWidth = 2;
        float4 shipColor = new float4(0.85f, 0.85f, 0.85f, 1);
        float2 shipTop = new float2(0,-shipHeight/2);
        float2 shipBL = new float2(-shipWidth/2,shipHeight/2);
        float2 shipBR = new float2(shipWidth/2,shipHeight/2);

        float2 thrustStart = new float2(0, shipHeight/2);
        float2 thrustEnd = new float2(0, shipHeight/2 + 20);

        for (int ship = 0; ship < spaceships.Length; ++ship)
        {

            float2 pos = LineRenderSystem.screenPosFromTransform(spaceships.transform[ship].position);
            var rot = spaceships.transform[ship].rotation;

            if (spaceships.input[ship].thrust > 0)
            {
                var rotTS = pos+LineRenderSystem.rotatePos(thrustStart, rot);
                var rotTE = pos+LineRenderSystem.rotatePos(thrustEnd, rot);
                float2 rotME = rotTS + (rotTE - rotTS)*0.5f;
                for (int i = 0; i < 3; ++i)
                {
                    float2 midOfs = new float2(Random.Range(-5, 5), Random.Range(-5, 5));
                    float2 endOfs = new float2(Random.Range(-2, 2), Random.Range(-2, 2));
                    lines.Add(new LineRenderSystem.Line(rotTS, rotME+midOfs, new float4(1, 1, 0.5f, 1), 1));
                    lines.Add(new LineRenderSystem.Line(rotME+midOfs, rotTE+endOfs, new float4(1, 1, 0.5f, 1), 1));
                }
            }

            var rotTop = pos+LineRenderSystem.rotatePos(shipTop, rot);
            var rotBL = pos+LineRenderSystem.rotatePos(shipBL, rot);
            var rotBR = pos+LineRenderSystem.rotatePos(shipBR, rot);
            lines.Add(new LineRenderSystem.Line(rotTop, rotBL, shipColor, shipLineWidth));
            lines.Add(new LineRenderSystem.Line(rotTop, rotBR, shipColor, shipLineWidth));
            lines.Add(new LineRenderSystem.Line(rotBL, rotBR, shipColor, shipLineWidth));
        }
    }
}