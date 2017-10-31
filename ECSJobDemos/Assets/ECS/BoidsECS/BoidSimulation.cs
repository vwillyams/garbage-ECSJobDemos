using UnityEngine;
using Unity.Collections;
using UnityEngine.ECS;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;

namespace BoidSimulations
{
	public struct BoidSimulationState
	{
		public float3 							ground;
		public float 							deltaTime;
	}

	[System.Serializable]
	public struct BoidSimulationSettings : IComponentData
	{
		public float speed;
		public float rotationalSpeed;

		public float cellRadius;

		public float separationWeight;
		public float alignmentWeight;
		public float targetWeight;

		public float groundAversionDistance;

		public void Init ()
		{
			speed = 6;
			rotationalSpeed = 5;
			cellRadius = 0.5F;
			separationWeight = 12;
			alignmentWeight = 8;
			targetWeight = 8;
			groundAversionDistance = 50;
		}

		static float3 CalculateNormalizedTargetDirection(float3 position, NativeArray<float3> targetPositions)
		{
			float closestDistance = math.distance (position, targetPositions[0]);
			int closestIndex = 0;
			for (int i = 1; i < targetPositions.Length; i++)
			{
				float distance = math.distance (position, targetPositions[i]);
				if (distance < closestDistance)
				{
					closestIndex = i;
					closestDistance = distance;
				}
			}

			return (targetPositions[closestIndex] - position ) / math.max(0.0001F, closestDistance);
		}

		static float3 AvoidObstacle (BoidObstacle obstacle, float3 position, float3 steer)
		{
			// avoid obstacle
			float3 obstacleDelta1 = obstacle.position - position;
			float sqrDist = math.dot(obstacleDelta1, obstacleDelta1);
			float orad = obstacle.size + obstacle.aversionDistance;
			if (sqrDist < orad * orad)
			{
				float dist = math.sqrt(sqrDist);
				float3 obs1Dir = obstacleDelta1 / dist;
				float a = dist - obstacle.size;
				if (a < 0)
					a = 0;
				float f = a / obstacle.aversionDistance;
				steer = steer + (-obs1Dir - steer) * (1 - f);
				steer = math_experimental.normalizeSafe(steer);
			}
			return steer;
		}

		public void CalculateSeparationAndAlignment(NativeArray<BoidData> boids, int index, NativeMultiHashMap<int, int> cells, NativeArray<int> cellOffsetsTable, out float3 alignmentSteering, out float3 separationSteering)
		{
			BoidData thisb = boids[index];
			separationSteering = new float3(0);
			alignmentSteering = new float3(0);


			int hash;
			int3 gridPos = HashUtility.Quantize(thisb.position, cellRadius);
			for (int oi = 0; oi < 7; oi++)
			{
				var gridOffset = new int3(cellOffsetsTable[oi++], cellOffsetsTable[oi++], cellOffsetsTable[oi++]);

				hash = HashUtility.Hash(gridPos + gridOffset);
				int i;

				NativeMultiHashMapIterator<int> iterator;
				bool found = cells.TryGetFirstValue(hash, out i, out iterator);
				int neighbors = 0;
				while (found && neighbors < 2)        // limit neighbors to help initial hiccup due to all boids starting from same point
				{
					if (i == index)
					{
						found = cells.TryGetNextValue(out i, ref iterator);
						continue;
					}
					neighbors++;
					BoidData other = boids[i];

					// add in steering contribution
					// (opposite of the offset direction, divided once by distance
					// to normalize, divided another time to get 1/d falloff)
					float3 offset = other.position - (thisb.position + thisb.forward * 0.5f);

					// should we have sqrLength?
					float distanceSquared = math.dot(offset, offset);
					separationSteering += (offset / -distanceSquared);

					// accumulate sum of neighbor's heading
					alignmentSteering += other.forward;

					found = cells.TryGetNextValue(out i, ref iterator);
				}
			}

			separationSteering = math_experimental.normalizeSafe(separationSteering);
			alignmentSteering = math_experimental.normalizeSafe(alignmentSteering);
		}

		public BoidData Steer(int index, BoidSimulationState state, NativeArray<BoidData> boids, NativeArray<BoidObstacle> boidObstacles, NativeArray<float3> targetPositions, NativeMultiHashMap<int, int> cells, NativeArray<int> cellOffsetsTable)
		{
			float3 separationSteering;
			float3 alignmentSteering;

			BoidData thisb = boids[index];

			CalculateSeparationAndAlignment (boids, index, cells, cellOffsetsTable, out alignmentSteering, out separationSteering);

			float3 targetDir = CalculateNormalizedTargetDirection (thisb.position, targetPositions);

			float3 steer = separationSteering * separationWeight + alignmentSteering * alignmentWeight + targetDir * targetWeight;
			steer = math_experimental.normalizeSafe(steer);

			for (int i = 0;i != boidObstacles.Length;i++)
				steer = AvoidObstacle (boidObstacles[i], thisb.position, steer);

			// avoid ground
			float height = thisb.position.y - state.ground.y;
			if (height < groundAversionDistance && steer.y < 0)
			{
				steer.y *= (height / groundAversionDistance);
			}
			steer = math_experimental.normalizeSafe(steer);

			BoidData thisData;
			thisData.forward = math_experimental.normalizeSafe(thisb.forward + steer * state.deltaTime * Mathf.Deg2Rad * rotationalSpeed);
			thisData.position = thisb.position + thisData.forward * state.deltaTime * speed;

			return thisData;
		}

		public static void AddHashCell (BoidData boidData, int index, float cellRadius, NativeMultiHashMap<int, int>.Concurrent cells)
		{
			var hash = HashUtility.Hash(boidData.position, cellRadius);
			cells.Add(hash, index);
		}
		public static void AddHashCell (BoidData boidData, int index, float cellRadius, NativeMultiHashMap<int, int> cells)
		{
			var hash = HashUtility.Hash(boidData.position, cellRadius);
			cells.Add(hash, index);
		}

		static bool InBoidNeighborhood (BoidData me, BoidData other, float minDistance, float maxDistance, float cosMaxAngle)
		{
			float3 offset = other.position - me.position;
			float distanceSquared = math.dot(offset, offset);

			// definitely in neighborhood if inside minDistance sphere
			if (distanceSquared < (minDistance * minDistance))
			{
				return true;
			}
			else
			{
				// definitely not in neighborhood if outside maxDistance sphere
				if (distanceSquared > (maxDistance * maxDistance))
				{
					return false;
				}
				else
				{
					// otherwise, test angular offset from forward axis
					float3 unitOffset = offset / math.sqrt(distanceSquared);
					float forwardness = math.dot( me.forward, unitOffset );
					return forwardness > cosMaxAngle;
				}
			}
		}
	}
}