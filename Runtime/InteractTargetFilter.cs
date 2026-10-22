using System.Collections.Generic;
using TW.CullingGroups;
using TW.CullingGroups.Conditions;
using Unity.Collections;
using UnityEngine;

namespace TW.Interaction.Prompts
{

	public class InteractTargetFilter : MonoBehaviour
	{
		[SerializeField]
		private CullingGroupFilter _cullingFilter;
		[SerializeField] PromptViewBase _pairManager;
		public LayerMask layerMask;
		public string m_IgnoreTag = string.Empty;
		[SerializeField] int _maxHits = 8;

		[SerializeField, Range(-1.0f, 1.0f)] private float angleThreshold = -1f;
		[SerializeField] List<Transform> _perspectives = new();

		private Camera _cam;
		private NativeArray<CullingGroupState> _cullingStates;
		private NativeArray<Vector3> _origins;
		NativeArray<EntityId> _allColliders;

		private readonly Dictionary<EntityId, EntityId[]> _trackedGroups = new();
		private int totalColliders = 0;


		private void Awake()
		{
			_cullingStates = new NativeArray<CullingGroupState>(0, Allocator.Persistent);
			_allColliders = new NativeArray<EntityId>(totalColliders, Allocator.Persistent);
			_cam = _cullingFilter.Profile.TargetCamera;
			_perspectives.Add(_cam.transform);
			_origins = new NativeArray<Vector3>(_perspectives.Count, Allocator.Persistent);
			for (int i = 0; i < _perspectives.Count; i++)
			{
				_origins[i] = _perspectives[i].position;
			}

		}

		private void OnEnable()
		{
			LoadingGroup();

			_cullingFilter.OnAdded += OnAdded;
			_cullingFilter.OnRemoved += OnRemoved;
		}

		private void OnDisable()
		{

			_cullingFilter.OnAdded -= OnAdded;
			_cullingFilter.OnRemoved -= OnRemoved;
		}

		private void FixedUpdate()
		{
			ObstacleCheck();
		}

		private void OnDestroy()
		{
			if (_cullingStates.IsCreated)
			{
				_cullingStates.Dispose();
			}
			if (_origins.IsCreated)
			{
				_origins.Dispose();
			}
			if (_allColliders.IsCreated)
			{
				_allColliders.Dispose();
			}
		}


		private void LoadingGroup()
		{
			if (!_cullingFilter)
			{
				return;
			}
			var profile = _cullingFilter.Profile;
			totalColliders = 0;
			_trackedGroups.Clear();
			foreach (var item in _cullingFilter.Filtered)
			{
				OnAdded(item, profile);
			}
		}

		private void OnAdded(EntityId id, CullingGroupProfile profile)
		{
			var target = profile.TargetDict[id];
			var colliders = target.GetColliderIds();
			if (colliders == null || colliders.Length == 0)
			{
				return;
			}

			bool found = _trackedGroups.TryGetValue(id, out var tGroup);
			if (found && colliders == tGroup)
			{
				return;
			}
			_trackedGroups[id] = colliders;
			int diff = 0;
			if (!found)
			{
				diff = colliders.Length;
				InteractPromptUtil.ResizeNativeArray(ref _cullingStates, _trackedGroups.Count, Allocator.Persistent);
			}
			else if (colliders.Length != tGroup.Length)
			{
				diff = tGroup.Length - colliders.Length;
			}
			if (diff == 0)
			{
				return;
			}
			totalColliders += diff;
			RefreshCollidersArray();
		}

		private void OnRemoved(EntityId id, CullingGroupProfile arg2)
		{
			if (!_trackedGroups.TryGetValue(id, out var group))
			{
				return;
			}

			_trackedGroups.Remove(id);

			totalColliders -= group.Length;
			RefreshCollidersArray();
			InteractPromptUtil.ResizeNativeArray(ref _cullingStates, _trackedGroups.Count, Allocator.Persistent);
		}

		private void RefreshCollidersArray()
		{
			if (_allColliders.Length != totalColliders)
			{
				InteractPromptUtil.ResizeNativeArray(ref _allColliders, totalColliders, Allocator.Persistent);
			}
			int count = 0;

			foreach (var value in _trackedGroups.Values)
			{
				for (int i = 0; i < value.Length; i++)
				{
					_allColliders[count + i] = value[i];
				}
				count += value.Length;
			}
		}

		public void AddPerspective(Transform perspective)
		{
			if (perspective == null || _perspectives.Contains(perspective))
			{
				return;
			}
			_perspectives.Add(perspective);
			InteractPromptUtil.ResizeNativeArray(ref _origins, _perspectives.Count, Allocator.Persistent);
		}

		public void RemovePerspective(Transform perspective)
		{
			if (perspective == null || !_perspectives.Contains(perspective))
			{
				return;
			}
			_perspectives.Remove(perspective);
			InteractPromptUtil.ResizeNativeArray(ref _origins, _perspectives.Count, Allocator.Persistent);

		}

		private void ObstacleCheck()
		{
			int colliderIndex = 0;
			int count = 0;
			for (int i = 0; i < _origins.Length; i++)
			{
				_origins[i] = _perspectives[i].transform.position;
			}
			foreach (var (key, value) in _trackedGroups)
			{
				var target = _cullingFilter.Profile.TargetDict[key];

				var state = target.GetState();

				state.colliderIndex = colliderIndex;
				_cullingStates[count] = state;
				colliderIndex += value.Length;
				count++;
			}
			var obstructions = new NativeArray<bool>(count, Allocator.TempJob);
			InteractPromptUtil.UpdateTacking(layerMask, m_IgnoreTag, _origins, _cullingStates, obstructions, _allColliders, _maxHits);
			EntityId closest = EntityId.None;
			float closestAngle = -1f;
			var camPos = _cam.transform.position;
			var camForward = _cam.transform.forward;
			for (int i = 0; i < obstructions.Length; i++)
			{
				var state = _cullingStates[i];
				bool isObstructed = obstructions[i];
				_pairManager.SetObstructed(state.claimId, isObstructed);
				if (state.groupEvent.currentDistance != 0 || isObstructed)
				{
					continue;
				}

				var direct = state.Position - camPos;
				var angle = Vector3.Dot(direct.normalized, camForward);
				if (angle > closestAngle)
				{
					closest = state.claimId;
					closestAngle = angle;
				}
			}
			if (closestAngle <= angleThreshold)
			{
				closest = EntityId.None;
			}
			_pairManager.SetTarget(closest);
			obstructions.Dispose();
		}
	}
}

