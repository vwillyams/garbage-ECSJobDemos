using Unity;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;

[UpdateBefore(typeof(LineRenderSystem))]
public class BulletRenderSystem : ComponentSystem
{
    [Inject]
    LineRenderSystem m_LineRenderSystem;

    struct Bullet
    {
        public int Length;
        [ReadOnly]
        public ComponentDataArray<BulletTagComponentData> tag;
        public ComponentArray<Transform> transform;
    }

    [InjectComponentGroup]
    Bullet bullets;

    override protected void OnUpdate()
    {
        NativeList<LineRenderSystem.Line> lines = m_LineRenderSystem.LineList;
        float bulletWidth = 2;
        float bulletLength = 2;
        float trailWidth = 4;
        float trailLength = 4;
        float4 bulletColor = new float4((float)0xfc / (float)255, (float)0x0f / (float)255, (float)0xc0 / (float)255, 1);
        float4 trailColor = new float4((float)0xfc / (float)255, (float)0x0f / (float)255, (float)0xc0 / (float)255, 0.25f);
        float2 bulletTop = new float2(0,-bulletLength/2);
        float2 bulletBottom = new float2(0,bulletLength/2);
        float2 trailBottom = new float2(0,trailLength);

        for (int bullet = 0; bullet < bullets.Length; ++bullet)
        {
            float2 pos = LineRenderSystem.screenPosFromTransform(bullets.transform[bullet].position);
            var rot = bullets.transform[bullet].rotation;
            var rotTop = pos+LineRenderSystem.rotatePos(bulletTop, rot);
            var rotBot = pos+LineRenderSystem.rotatePos(bulletBottom, rot);
            var rotTrail = pos+LineRenderSystem.rotatePos(trailBottom, rot);
            lines.Add(new LineRenderSystem.Line(rotTop, rotBot, bulletColor, bulletWidth));
            lines.Add(new LineRenderSystem.Line(rotTop, rotTrail, trailColor, trailWidth));
        }
    }
}