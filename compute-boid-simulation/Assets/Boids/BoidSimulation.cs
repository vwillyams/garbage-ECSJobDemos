using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental;
using UnityEngine.Serialization;
using Unity.Jobs;
using UnityEngine.Collections;

namespace BoidcloudSimulation
{

	// Upgrade from Vector3 to float3 notes:
	// * sqrMagnitude / sqrDistance func?
	// * Vector3.forward, Vector3.right, Vector3.up etc

	// TransformAccess API:
	// * Nan checks

	public struct BoidData
	{
		public float3 position;
		public float3 forward;
	}

	public struct BoidSimulationState
	{
		public float3 							target1;
		public float3 							target2;
		public float3 							ground;
		public float3 							obstacle1;
		public float 							deltaTime;
	}

	[System.Serializable]
	public struct BoidSimulationSettings
	{
		public float speed;
		public float rotationalSpeed;

		public float radius;

		public float separationWeight;
		public float alignmentWeight;
		public float targetWeight;

	    public float groundAversionDistance;
	    public float obstacle1Size;
	    public float obstacle1AversionDistance;

	    public void Init ()
		{
			speed = 6;
			rotationalSpeed = 5;
			radius = 0.5F;
			separationWeight = 12;
			alignmentWeight = 8;
			targetWeight = 8;
	        groundAversionDistance = 50;
	        obstacle1Size = 30;
	        obstacle1AversionDistance = 30;
		}

		public BoidData Steer(int index, BoidSimulationState state, NativeArray<BoidData> boids, NativeMultiHashMap<int, int> cells, NativeArray<int> cellOffsetsTable)
		{
	        var separationSteering = new float3(0);
	        var alignmentSteering = new float3(0);

			BoidData thisb = boids[index];

			int hash;
	        int3 gridPos = HashUtility.Quantize(thisb.position, radius);
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

			float3 targetDelta1 = state.target1 - thisb.position;
			float3 targetDelta2 = state.target2 - thisb.position;

			float targetDelta1Length = math.length(targetDelta1);
			float targetDelta2Length = math.length(targetDelta2);

			float3 targetDelta = (targetDelta1Length < targetDelta2Length) ? (targetDelta1 / targetDelta1Length) : (targetDelta2 / targetDelta2Length);

	        float3 steer = separationSteering * separationWeight + alignmentSteering * alignmentWeight + targetDelta * targetWeight;
			steer = math_experimental.normalizeSafe(steer);

			// avoid obstacle
			float3 obstacleDelta1 = state.obstacle1 - thisb.position;
			float sqrDist = math.dot(obstacleDelta1, obstacleDelta1);
			float orad = obstacle1Size + obstacle1AversionDistance;
			if (sqrDist < orad * orad)
			{
				float dist = math.sqrt(sqrDist);
				float3 obs1Dir = obstacleDelta1 / dist;
				float a = dist - obstacle1Size;
	            if (a < 0)
	                a = 0;
				float f = a / obstacle1AversionDistance;
				steer = steer + (-obs1Dir - steer) * (1 - f);
				steer = math_experimental.normalizeSafe(steer);
	        }

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

		public static void AddHashCell (BoidData boidData, int index, float radius, NativeMultiHashMap<int, int>.Concurrent cells)
		{
			var hash = HashUtility.Hash(boidData.position, radius);
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