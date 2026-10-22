using TW.CullingGroups;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TW.Interaction.Prompts
{

	public static class InteractPromptUtil
	{

		public static void UpdateTacking(
			LayerMask layerMask,
			string ignoreTag,
			NativeArray<Vector3> origins,
			NativeArray<CullingGroupState> cullingObjects,
			NativeArray<bool> obstructions,
			NativeArray<EntityId> colliders,
			int maxHits = 8
			)
		{
			int originCount = origins.Length;
			int count = obstructions.Length;

			for (int i = 0; i < originCount; i++)
			{
				var commands = new NativeArray<RaycastCommand>(count, Allocator.TempJob);
				int commandCount = 0;
				var tracking = new NativeArray<int>(count, Allocator.TempJob);
				int trackingIndex = 0;
				var origin = origins[i];
				for (int j = 0; j < count; j++)
				{
					var obj = cullingObjects[j];

					tracking[trackingIndex] = j;
					trackingIndex++;
					var heading = obj.Position - origin;
					commands[commandCount] = new RaycastCommand(origin, heading.normalized, new QueryParameters(layerMask), heading.sqrMagnitude);
					commandCount++;
				}

				if (commandCount == 0)
				{
					commands.Dispose();
					tracking.Dispose();
					continue;
				}
				if (commandCount != count)
				{
					ResizeNativeArray(ref commands, commandCount, Allocator.TempJob);
				}
				var filteredResults = new NativeArray<RaycastHit>(commandCount, Allocator.TempJob);
				TagCommandUtil.RaycastCommandsIgnoreTag(commands, filteredResults, ignoreTag, maxHits);

				var applyObstacles = new ApplyObstructedJob()
				{
					Obstructions = obstructions,
					CullingObjects = cullingObjects,
					hits = filteredResults,
					tracking = tracking,
					colliders = colliders,
				};
				applyObstacles.Schedule(commandCount, 64).Complete();

				for (int j = 0; j < count; j++)
				{
					Debug.DrawLine(origin, cullingObjects[j].Position, obstructions[j] ? Color.red : Color.green);
				}
			}

		}

		public static void ResizeNativeArray<T>(ref NativeArray<T> array, int newSize, Allocator allocator) where T : struct
		{
			if (newSize < 0)
				throw new System.ArgumentOutOfRangeException(nameof(newSize), "Size must be non-negative.");

			NativeArray<T> newArray = new(newSize, allocator);

			if (array.IsCreated)
			{
				int copyLength = math.min(array.Length, newSize);
				NativeArray<T>.Copy(array, newArray, copyLength);
				array.Dispose();
			}

			array = newArray;
		}



		//[BurstCompile]
		//struct PriorityJob : IJobParallelFor
		//{
		//	[ReadOnly] public NativeArray<CullingObject> CullingObjects;
		//	public NativeArray<float> Angles;
		//	public NativeArray<int> Indices;
		//	public Vector3 SourcePosition;
		//	public Vector3 SourceDirection;

		//	public void Execute(int i)
		//	{
		//		Vector3 direct = CullingObjects[i].Position - SourcePosition;
		//		float angle = Vector3.Dot(direct.normalized, SourceDirection);
		//		if (angle > Angles[0])
		//		{
		//			Angles[0] = angle;
		//			Indices[0] = i;
		//		}
		//	}
		//}
	}

	[BurstCompile]
	struct ApplyObstructedJob : IJobParallelFor
	{
		[WriteOnly] public NativeArray<bool> Obstructions;
		[ReadOnly] public NativeArray<CullingGroupState> CullingObjects;
		[ReadOnly] public NativeArray<EntityId> colliders;
		[ReadOnly, DeallocateOnJobCompletion] public NativeArray<RaycastHit> hits;
		[ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> tracking;
		public void Execute(int i)
		{
			int index = tracking[i];
			var co = CullingObjects[index];
			var hit = hits[i];
			var id = hit.colliderEntityId;
			if (!id.IsValid())
			{
				return;
			}
			if (co.colliderCount == 0)
			{
				Obstructions[index] = true;
			}
			else
			{
				int length = co.colliderCount + co.colliderIndex;
				bool found = false;
				for (int j = co.colliderIndex; j < length; j++)
				{
					if (id == colliders[j])
					{
						found = true;
						break;
					}
				}
				if (!found)
				{
					Obstructions[index] = true;
				}
			}
		}
	}
}
