using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TW.Interaction.Prompts
{

	public class TagCommandUtil
	{
		public static (JobHandle, NativeArray<RaycastHit>, NativeArray<RaycastCommand>) RaycastCommandIgnoreTag(Vector3 position, Vector3 direction, float distance, LayerMask layerMask, string tag, int maxHits = 16, JobHandle dependsOn = default)
		{
			int count = 1;

			NativeArray<RaycastHit> results = new(maxHits, Allocator.TempJob);
			NativeArray<RaycastCommand> commands = new(count, Allocator.TempJob);

			QueryParameters param = new(layerMask);
			commands[0] = new RaycastCommand(position, direction, param, distance);

			// Schedule the batch of raycasts
			JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, count, maxHits, dependsOn);

			return (handle, results, commands);
		}

		public static void ProcessCommandIgnoreTag(string tag, NativeArray<RaycastHit> results, out RaycastHit hit)
		{
			int index = -1;
			for (int i = 0; i < results.Length; i++)
			{
				var coll = results[i].collider;
				if (!coll)
				{
					continue;
				}
				if (coll.gameObject.CompareTag(tag))
				{
					continue;
				}
				index = i;
				break;
			}
			hit = index != -1 ? results[index] : new RaycastHit();

			// Dispose the buffers
			results.Dispose();
		}

		public static void ProcessCommandsIgnoreTag(string tag, NativeArray<RaycastHit> results, List<RaycastHit> hits, int maxHits)
		{
			int count = (int)(results.Length / (float)maxHits);
			for (int j = 0; j < count; j++)
			{
				int index = -1;

				for (int i = 0; i < maxHits; i++)
				{
					int k = (maxHits * j) + i;
					var coll = results[k].collider;
					if (!coll)
					{
						continue;
					}
					if (coll.gameObject.CompareTag(tag))
					{
						continue;
					}
					index = k;
					break;
				}
				hits.Add(index != -1 ? results[index] : new RaycastHit());
			}

			// Dispose the buffers
			results.Dispose();
		}

		public static void ProcessCommandsIgnoreTag(string tag, NativeArray<RaycastHit> results, NativeArray<RaycastHit> hits, int maxHits)
		{
			bool emptyTag = string.IsNullOrEmpty(tag);
			int count = (int)(results.Length / (float)maxHits);
			for (int j = 0; j < count; j++)
			{
				int index = -1;
				float dist = float.MaxValue;

				for (int i = 0; i < maxHits; i++)
				{
					int k = (maxHits * j) + i;
					var result = results[k];
					if (!result.colliderEntityId.IsValid())
					{
						break;
					}
					var coll = result.collider;
					if (!emptyTag && coll.gameObject.CompareTag(tag))
					{
						continue;
					}
					if (result.distance < dist)
					{
						dist = result.distance;
						index = k;
					}
					//break;
				}
				hits[j] = index != -1 ? results[index] : new RaycastHit();

			}

			// Dispose the buffers
			results.Dispose();
		}

		public static void RaycastCommandIgnoreTag(Ray ray, out RaycastHit hit, float distance, LayerMask layerMask, in string tag, int maxHits = 16)
		{
			int count = 1;

			NativeArray<RaycastHit> results = new(maxHits, Allocator.TempJob);
			NativeArray<RaycastCommand> commands = new(count, Allocator.TempJob);

			QueryParameters param = new(layerMask);
			commands[0] = new RaycastCommand(ray.origin, ray.direction, param, distance);

			// Schedule the batch of raycasts
			JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, count, maxHits);

			// Wait for the batch processing job to complete
			handle.Complete();
			commands.Dispose();
			ProcessCommandIgnoreTag(tag, results, out hit);

		}

		public static void RaycastCommandsIgnoreTag(List<(Ray ray, LayerMask layerMask, float distance)> instructions, List<RaycastHit> hits, in string tag, int maxHits = 16)
		{
			int count = instructions.Count;
			int totalHits = maxHits * count;
			if (totalHits == 0)
			{
				return;
			}
			NativeArray<RaycastHit> results = new(totalHits, Allocator.TempJob);
			NativeArray<RaycastCommand> commands = new(count, Allocator.TempJob);
			for (int i = 0; i < count; i++)
			{
				(Ray ray, LayerMask layerMask, float distance) = instructions[i];
				QueryParameters param = new(layerMask);
				commands[i] = new RaycastCommand(ray.origin, ray.direction, param, distance);
			}

			// Schedule the batch of raycasts
			JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, count, maxHits);

			// Wait for the batch processing job to complete
			handle.Complete();
			commands.Dispose();
			ProcessCommandsIgnoreTag(tag, results, hits, maxHits);

		}

		public static void RaycastCommandsIgnoreTag(NativeArray<RaycastCommand> commands, NativeArray<RaycastHit> hits, in string tag, int maxHits = 16)
		{
			int totalHits = maxHits * commands.Length;
			if (totalHits == 0)
			{
				return;
			}
			NativeArray<RaycastHit> results = new(totalHits, Allocator.TempJob);

			// Schedule the batch of raycasts
			JobHandle rayHandle = RaycastCommand.ScheduleBatch(commands, results, commands.Length, maxHits);
			//var filterJob = new FilterHitJob
			//{
			//	Results = hits,
			//	Hits = results,
			//	maxHits = maxHits,
			//	tag = new FixedString128Bytes(tag)

			//};

			//var handle = filterJob.Schedule(hits.Length, 64, rayHandle);
			//handle.Complete();
			rayHandle.Complete();
			commands.Dispose();
			//results.Dispose();
			ProcessCommandsIgnoreTag(tag, results, hits, maxHits);

		}

		[BurstCompile]
		struct FillCommandsJob : IJobParallelFor
		{
			[ReadOnly] public NativeArray<Vector3> Origins;
			[ReadOnly] public NativeArray<Vector3> Directions;
			[ReadOnly] public QueryParameters QueryParameters;
			public NativeArray<RaycastCommand> Commands;

			public void Execute(int i)
			{
				Commands[i] = new RaycastCommand(Origins[i], Directions[i], QueryParameters);
			}
		}

	}
}
