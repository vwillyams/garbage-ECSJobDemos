using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;
using UnityEngine.ECS.SimpleBounds;
using UnityEngine.ECS.Transform2D;

namespace UnityEngine.ECS.Boids
{
    public class Boid2DSystem : JobComponentSystem
    {
        NativeMultiHashMap<int, int> 		 m_Cells;
        NativeArray<int2> 					 m_CellOffsetsTable;
        NativeArray<float>                   m_Bias;

        struct BoidSettingsGroup
        {
            [ReadOnly] public ComponentDataArray<BoidSettings> settings;
            public int Length;
        }

        [Inject] private BoidSettingsGroup m_BoidSettingsGroup;

        struct BoidGroup
        {
            [ReadOnly] public ComponentDataArray<Boid>       boid; 
            [ReadOnly] public ComponentDataArray<Position2D> positions;
            public ComponentDataArray<Heading2D>             headings;
            public int Length;
        }

        [Inject] private BoidGroup m_BoidGroup;

        struct ObstacleGroup
        {
            [ReadOnly] public ComponentDataArray<BoidObstacle> obstacles;
            [ReadOnly] public ComponentDataArray<Radius>       spheres;
            [ReadOnly] public ComponentDataArray<Position2D>   positions;
            public int Length;
        }

        [Inject] private ObstacleGroup m_ObstacleGroup;

        struct TargetGroup
        {
            [ReadOnly] public ComponentDataArray<Position2D> positions;
            [ReadOnly] public ComponentDataArray<BoidTarget> target;
            public int Length;
        }

        [Inject] private TargetGroup m_TargetGroup;
        
        [ComputeJobOptimization]
        struct HashBoidLocations : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position2D> positions;
            public NativeMultiHashMap<int, int>.Concurrent   cells;
            public float 				     			     cellRadius;

            public void Execute(int index)
            {
                var hash = HashUtility.Hash(positions[index].position, cellRadius);
                cells.Add(hash, index);
            }
        }
        
        [ComputeJobOptimization]
        struct Steer : IJob
        {
            public ComponentDataArray<Heading2D>                 headings;
            [ReadOnly] public ComponentDataArray<Position2D>     positions;
            [ReadOnly] public ComponentDataArray<Position2D>     targetPositions;
            [ReadOnly] public ComponentDataArray<Position2D>     obstaclePositions;
            [ReadOnly] public ComponentDataArray<BoidObstacle>   obstacles;
            [ReadOnly] public ComponentDataArray<Radius>         obstacleSpheres;
            [ReadOnly] public NativeMultiHashMap<int, int>       cells;
            [ReadOnly] public NativeArray<int2> 				 cellOffsetsTable;
            [ReadOnly] public BoidSettings                       settings;
            [ReadOnly] public NativeArray<float>                 bias;
            public float                                         dt;
            
            static float2 AvoidObstacle (float2 obstaclePosition, float obstacleRadius, BoidObstacle obstacle, float2 position, float2 steer, float aversionDistance)
            {
                // avoid obstacle
                float2 obstacleDelta1 = obstaclePosition - position;
                float sqrDist = math.dot(obstacleDelta1, obstacleDelta1);
                float orad = obstacleRadius + aversionDistance;
                if (sqrDist < orad * orad)
                {
                    float dist = math.sqrt(sqrDist);
                    float2 obs1Dir = obstacleDelta1 / dist;
                    float a = dist - obstacleRadius;
                    if (a < 0)
                        a = 0;
                    float f = a / aversionDistance;
                    steer = steer + (-obs1Dir - steer) * (1 - f);
                    steer = math_experimental.normalizeSafe(steer);
                }
                return steer;
            } 
            
            float2 CalculateNormalizedTargetDirection(int index)
            {
                var position = positions[index].position;
                float closestDistance = math.distance (position, targetPositions[0].position);
                int closestIndex = 0;
                for (int i = 1; i < targetPositions.Length; i++)
                {
                    float distance = math.distance (position, targetPositions[i].position);
                    if (distance < closestDistance)
                    {
                        closestIndex = i;
                        closestDistance = distance;
                    }
                }

                return (targetPositions[closestIndex].position - position ) / math.max(0.0001F, closestDistance);
            }
            
            void CalculateSeparationAndAlignment(int index, out float2 separationSteering, out float2 alignmentSteering )
            {
                var position = positions[index].position;
                var forward = headings[index].heading;
                
                separationSteering = new float2(0);
                alignmentSteering = new float2(0);
                
                int hash;
                var gridPos = HashUtility.Quantize(position, settings.cellRadius);
                for (int oi = 0; oi < 4; oi++)
                {
                    var gridOffset = cellOffsetsTable[oi];

                    hash = HashUtility.Hash(gridPos + gridOffset);
                    int i;

                    NativeMultiHashMapIterator<int> iterator;
                    bool found = cells.TryGetFirstValue(hash, out i, out iterator);
                    int neighbors = 0;
                    while (found)
                    {
                        if (i == index)
                        {
                            found = cells.TryGetNextValue(out i, ref iterator);
                            continue;
                        }
                        neighbors++;

                        var otherPosition = positions[i].position;
                        var otherForward = headings[i].heading;

                        // add in steering contribution
                        // (opposite of the offset direction, divided once by distance
                        // to normalize, divided another time to get 1/d falloff)
                        var offset = otherPosition - (position + forward * bias[index&1023] );

                        var distanceSquared = math.lengthSquared(offset);
                        separationSteering += (offset / -distanceSquared);

                        // accumulate sum of neighbor's heading
                        alignmentSteering += otherForward;

                        found = cells.TryGetNextValue(out i, ref iterator);
                    }
                }

                separationSteering = math_experimental.normalizeSafe(separationSteering);
                alignmentSteering = math_experimental.normalizeSafe(alignmentSteering);
            }

            public void Execute()
            {
                for (int index = 0; index < headings.Length; index++)
                {
                    var forward = headings[index].heading;
                    var position = positions[index].position;
                    
                    float2 alignmentSteering;
                    float2 separationSteering;

                    CalculateSeparationAndAlignment(index, out alignmentSteering, out separationSteering);

                    var targetSteering = CalculateNormalizedTargetDirection(index);
                    
                    var obstacleSteering = forward;
                    for (int i = 0;i != obstacles.Length;i++)
                        obstacleSteering = AvoidObstacle (obstaclePositions[i].position, obstacleSpheres[i].radius, obstacles[i], position, obstacleSteering, settings.obstacleAversionDistance);

                    var steer = (alignmentSteering * settings.alignmentWeight) +
                                (separationSteering * settings.separationWeight) +
                                (targetSteering * settings.targetWeight) +
                                (obstacleSteering * settings.obstacleWeight);
                    
                    math_experimental.normalizeSafe(steer);

                    headings[index] = new Heading2D
                    {
                        heading = math_experimental.normalizeSafe(forward + steer * 2.0f * bias[index & 1023] * dt * Mathf.Deg2Rad * settings.rotationalSpeed )
                    };
                }
            }
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_BoidSettingsGroup.Length == 0)
            {
                return inputDeps;
            }

            if (m_BoidGroup.Length == 0)
            {
                return inputDeps;
            }
            
            // Only support one boid type until we can destroy m_Cells after jobs
            var settings = m_BoidSettingsGroup.settings[0];
            
            m_Cells.Capacity = math.max (m_Cells.Capacity, m_BoidGroup.Length);
            m_Cells.Clear();

            var hashBoidLocationsJob = new HashBoidLocations
            {
                positions = m_BoidGroup.positions,
                cells = m_Cells,
                cellRadius = settings.cellRadius
            };

            var hashBoidLocationsJobHandle = hashBoidLocationsJob.Schedule(m_BoidGroup.Length, 64, inputDeps);

            var steerJob = new Steer
            {
                headings = m_BoidGroup.headings,
                positions = m_BoidGroup.positions,
                cells = m_Cells,
                settings = settings,
                cellOffsetsTable = m_CellOffsetsTable,
                targetPositions = m_TargetGroup.positions,
                obstaclePositions = m_ObstacleGroup.positions,
                obstacles = m_ObstacleGroup.obstacles,
                obstacleSpheres = m_ObstacleGroup.spheres,
                bias = m_Bias,
                dt = Time.deltaTime
            };

            var steerJobHandle = steerJob.Schedule(hashBoidLocationsJobHandle);
                
            return steerJobHandle;
        }
        
        protected override void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            m_Cells = new NativeMultiHashMap<int, int>(capacity, Allocator.Persistent);
            m_CellOffsetsTable = new NativeArray<int2>(HashUtility.cell2DOffsets, Allocator.Persistent);
            m_Bias = new NativeArray<float>(1024,Allocator.Persistent);
            for (int i = 0; i < 1024; i++)
            {
                m_Bias[i] = Random.Range(0.5f, 0.6f);
            }
        }

        protected override void OnDestroyManager()
        {
            base.OnDestroyManager();
            m_Cells.Dispose ();
            m_CellOffsetsTable.Dispose();
            m_Bias.Dispose();
        }

    }
}
