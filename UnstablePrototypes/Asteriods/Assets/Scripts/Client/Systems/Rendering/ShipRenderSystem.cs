using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;

[UpdateBefore(typeof(ParticleEmitterSystem))]
public class ShipThrustParticleSystem : ComponentSystem
{
    struct Spaceships
    {
        public int Length;
        [ReadOnly]
        public ComponentDataArray<PlayerTagComponentData> self;
        [ReadOnly]
        public ComponentDataArray<PlayerInputComponentData> input;
        public ComponentDataArray<ParticleEmitterComponentData> emitter;
    }
    [InjectComponentGroup]
    Spaceships spaceships;

    override protected void OnUpdate()
    {
        for (int i = 0; i < spaceships.Length; ++i)
        {
            var emitter = spaceships.emitter[i];
            emitter.active = spaceships.input[i].thrust;
            spaceships.emitter[i] = emitter;
        }
    }
}

[UpdateBefore(typeof(LineRenderSystem))]
public class ShipRenderSystem : ComponentSystem
{
    [Inject]
    LineRenderSystem m_LineRenderSystem;

    struct Spaceships
    {
        public int Length;
        [ReadOnly]
        public ComponentDataArray<PlayerTagComponentData> self;
        [ReadOnly]
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

            var rotTop = pos+LineRenderSystem.rotatePos(shipTop, rot);
            var rotBL = pos+LineRenderSystem.rotatePos(shipBL, rot);
            var rotBR = pos+LineRenderSystem.rotatePos(shipBR, rot);
            lines.Add(new LineRenderSystem.Line(rotTop, rotBL, shipColor, shipLineWidth));
            lines.Add(new LineRenderSystem.Line(rotTop, rotBR, shipColor, shipLineWidth));
            lines.Add(new LineRenderSystem.Line(rotBL, rotBR, shipColor, shipLineWidth));
        }
    }
}