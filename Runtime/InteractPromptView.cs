using System.Collections.Generic;
using TW.CullingGroups;
using TW.CullingGroups.Conditions;
using TW.Interaction.Inputs;
using Unity.Properties;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace TW.Interaction.Prompts
{

	public class InteractPromptView : PromptViewBase
	{

		[SerializeField] private CullingGroupFilter _cullingFilter;
		[SerializeField] private InteractInputProvider _provider;
		[SerializeField] private UIDocument uiDocument;
		[SerializeField] private VisualTreeAssetPool pool;
		[SerializeField] private ContextPromptData _data;

		[SerializeField] Vector3 _offScreenPoint = new(0, 200);
		[SerializeField] bool _centerContext = false;
		[SerializeField] bool _centerContextWhenNotVisible = false;
		[SerializeField] Vector3 _contextOffset = new(0, 0);

		[Header("Element Paths")]
		[SerializeField] private string containerPathName = "BaseContainer";

		[Header("Icon Element Paths")]
		[SerializeField] private string itemContentPathName = "icon";

		[Header("Context Element Paths")]
		[SerializeField] private string contextPathName = "context";
		[SerializeField] private string contextTitlePathName = "title";
		[SerializeField] private string contextListViewPathName = "listView";
		[Header("Element Class Names")]
		[SerializeField] private string visibleClassName = "is-visible";
		[SerializeField] private string rangeClassName = "in-range";

		[SerializeField] private UnityEvent<InteractableObject> onTargetChanged = new();

		private VisualElement m_BaseContainer;
		private VisualElement m_Context;
		private readonly HashSet<EntityId> _exitQueue = new();
		private EntityId _targetEntityId;
		private InteractPromptBinder _target;
		private readonly Dictionary<EntityId, PromptPairState> _stateDict = new();
		public ContextPromptData Data => _data;
		public IReadOnlyDictionary<EntityId, PromptPairState> States => _stateDict;

		public InteractPromptBinder Target => _target;

		public VisualElement Context => m_Context;

		private void OnEnable()
		{

			if (uiDocument != null && uiDocument.rootVisualElement != null)
			{
				m_BaseContainer = uiDocument.rootVisualElement.Q<VisualElement>(containerPathName);
				m_Context = uiDocument.rootVisualElement.Q<VisualElement>(contextPathName);
				//var label = m_Context.Q<Label>(contextTitlePathName);
				//label.dataSource = _data;
				//label.SetBinding(nameof(Label.text), _data.Title);
				//BindContext();
			}

			_cullingFilter.OnAdded += OnFilterAdded;
			_cullingFilter.OnRemoved += OnFilterRemoved;
			_cullingFilter.Profile.OnCullingGroupEvent += OnCullingGroupEvent;

			Canvas.willRenderCanvases += WillRenderCanvases;
		}

		private void OnDisable()
		{
			m_BaseContainer = null;
			_cullingFilter.OnAdded -= OnFilterAdded;
			_cullingFilter.OnRemoved -= OnFilterRemoved;
			_cullingFilter.Profile.OnCullingGroupEvent -= OnCullingGroupEvent;

			Canvas.willRenderCanvases -= WillRenderCanvases;
			foreach (var state in _stateDict.Values)
			{
				pool.Release(state.rootUI);
				state.contentUI.EnableInClassList(rangeClassName, false);
			}
			if (_targetEntityId != EntityId.None)
			{
				_targetEntityId = EntityId.None;
				_target = null;
				if (_provider != null)
				{
					_provider.ClearTarget();
				}
				onTargetChanged.Invoke(null);
			}
			_stateDict.Clear();
			_exitQueue.Clear();
		}

		private void OnFilterRemoved(EntityId id, CullingGroupProfile arg2)
		{
			if (!_stateDict.TryGetValue(id, out var state))
			{
				return;
			}
			_exitQueue.Add(id);

			state.contentUI.RegisterCallbackOnce<TransitionEndEvent>(evt =>
			{
				Remove(id);
			});
			state.contentUI.EnableInClassList(visibleClassName, false);
		}

		private void Remove(EntityId id)
		{
			if (!_stateDict.TryGetValue(id, out var state))
			{
				return;
			}
			if (!_exitQueue.Contains(id))
			{
				return;
			}
			pool.Release(state.rootUI);
			state.contentUI.EnableInClassList(rangeClassName, false);
			if (id == _targetEntityId)
			{
				_targetEntityId = EntityId.None;
				_target = null;
				_provider.ClearTarget();
				onTargetChanged.Invoke(null);
			}
			_stateDict.Remove(id);
			_exitQueue.Remove(id);
		}

		private void OnFilterAdded(EntityId id, CullingGroupProfile profile)
		{
			if (_stateDict.TryGetValue(id, out var state))
			{
				_exitQueue.Remove(id);
				return;
			}
			var target = profile.TargetDict[id];
			state = GetState(target);
			_stateDict[id] = state;
			UpdateState(state, target.Current);
		}


		private void WillRenderCanvases()
		{

			foreach (var (key, state) in States)
			{
				var culling = state.binder.CullingGroupObject.Target;
				bool isVisible = culling.IsVisible;

				SetPosition(key, isVisible ? _cullingFilter.Profile.TargetCamera.WorldToViewportPoint(culling.Offset) : _offScreenPoint);
			}

		}

		private void OnCullingGroupEvent(CullingGroupEvent culling)
		{
			var target = _cullingFilter.Profile.Objects[culling.index];
			if (!_stateDict.TryGetValue(target.ClaimId, out PromptPairState state) || _exitQueue.Contains(target.ClaimId))
			{
				// Handle the state if needed
				return;
			}

			UpdateState(state, culling);
		}


		public void UpdateState(PromptPairState state, CullingGroupEvent culling)
		{
			bool isVisible = culling.isVisible && !state.isObstructed && _target != state.binder;

			state.contentUI.EnableInClassList(visibleClassName, isVisible);
			state.contentUI.EnableInClassList(rangeClassName, culling.currentDistance == 0);
		}


		private PromptPairState GetState(CullingGroupTarget target)
		{
			var prompt = pool.Pool.Get();
			prompt.usageHints = UsageHints.DynamicTransform;
			prompt.style.display = DisplayStyle.Flex;
			prompt.style.position = new StyleEnum<Position>(Position.Absolute);
			m_BaseContainer.Add(prompt);
			target.transform.TryGetComponent(out InteractPromptBinder binder);
			var content = prompt.Q(itemContentPathName);
			var state = new PromptPairState()
			{
				binder = binder,
				rootUI = prompt,
				contentUI = content
			};
			return state;
		}

		public void SetPosition(EntityId id, Vector3 screenPoint)
		{
			if (!_stateDict.TryGetValue(id, out var state))
			{
				return;
			}

			var prompt = state.rootUI;
			if (prompt == null)
			{
				return;
			}
			var containerSize = m_BaseContainer.layout.size;
			var output = new Vector3(screenPoint.x * containerSize.x, (1 - screenPoint.y) * containerSize.y, screenPoint.z);
			if (_target == state.binder)
			{
				m_Context.style.translate = _centerContext || (_centerContextWhenNotVisible && !state.binder.CullingGroupObject.IsVisible) ? new Translate(containerSize.x * 0.5f, containerSize.y * 0.5f) : new Translate(output.x, output.y);
			}

			prompt.style.translate = new Translate(output.x, output.y);
		}

		public bool TryGetStateByCullingIndex(int id, out PromptPairState state)
		{
			if (id < 0 || id >= _cullingFilter.Profile.Objects.Count)
			{
				state = new();
				return false;
			}
			var target = _cullingFilter.Profile.Objects[id];
			return _stateDict.TryGetValue(target.ClaimId, out state);
		}

		public void SetObstructByCullingID(int id, bool isObstructed)
		{
			if (id < 0 || id >= _cullingFilter.Profile.Objects.Count)
			{
				return;
			}
			var target = _cullingFilter.Profile.Objects[id];

			SetObstructed(target.ClaimId, isObstructed);

		}

		public override void SetObstructed(EntityId id, bool isObstructed)
		{
			if (!_stateDict.TryGetValue(id, out var state) || isObstructed == state.isObstructed)
			{
				return;
			}

			state.isObstructed = isObstructed;
			_stateDict[id] = state;
			UpdateState(state, state.binder.CullingGroupObject.Current);
		}

		public override void SetTarget(EntityId id)
		{
			if (id == EntityId.None && _target != null)
			{
				var old = _target;
				_target = null;
				if (old != null && _stateDict.TryGetValue(_targetEntityId, out var oldState))
				{
					UpdateState(oldState, oldState.binder.CullingGroupObject.Current);
				}
				_targetEntityId = EntityId.None;
				_data.Engaged = false;
				_provider.ClearTarget();
				onTargetChanged.Invoke(null);
				return;
			}
			if (!_stateDict.TryGetValue(id, out var state))
			{
				return;
			}
			if (_target == state.binder)
			{
				return;
			}

			_target = state.binder;
			_targetEntityId = id;
			UpdateState(state, state.binder.CullingGroupObject.Current);
			RefreshContext();

			_provider.SetTarget(_target.InteractableObject);
			onTargetChanged.Invoke(_target.InteractableObject);
		}

		public void RefreshContext()
		{
			_data.Engaged = _targetEntityId != EntityId.None;
			_data.Title = _target.InteractableObject.Title;
			int count = Mathf.Max(_data.ContextActions.Count, _target.InteractableObject.Activities.Count);
			for (int i = 0; i < count; i++)
			{
				if (i >= _provider.Inputs.Count)
				{
					break;
				}
				if (i >= _data.ContextActions.Count)
				{
					_data.ContextActions.Add(new ContextActionData()
					{
						InputIcon = _provider.Inputs[i].imageIcon,
						Verb = _target.InteractableObject.Activities[i].Verb
					});
				}
				else
				{
					_data.ContextActions[i].InputIcon = _provider.Inputs[i].imageIcon;
					_data.ContextActions[i].Verb = _target.InteractableObject.Activities[i].Verb;

				}
				if (i >= _target.InteractableObject.Activities.Count)
				{
					_data.ContextActions.RemoveAt(_data.ContextActions.Count - 1);

				}
			}
			//ApplyContext();
			BindContext();
		}

		private void BindContext()
		{
			if (uiDocument == null || uiDocument.rootVisualElement == null)
			{
				return;
			}

			var context = uiDocument.rootVisualElement.Q<VisualElement>(contextPathName);

			//context.dataSource = _data;
			//context.SetBinding("style.display", new DataBinding()
			//{
			//	dataSourcePath = new PropertyPath(nameof(ContextPromptData.Engaged)),
			//	bindingMode = BindingMode.ToTarget,
			//	dataSource = _data
			//});

			var label = context.Q<Label>(contextTitlePathName);
			//label.GetBinding(nameof(Label.text)).MarkDirty();
			label.SetBinding(nameof(Label.text), _data.Title);

			var listView = context.Q<ListView>(contextListViewPathName);
			//listView.itemsSource = _data.ContextActions;
			listView.bindItem = (element, index) =>
			{
				var btn = element.Q<Button>();
				var verb = _data.ContextActions[index];
				//label.SetBinding(nameof(Label.text), verb.Verb);
				btn.SetBinding(nameof(Button.text), new DataBinding()
				{
					dataSourcePath = new PropertyPath(nameof(ContextActionData.VerbString))
				});
				//var image = element.Q<Image>();
				btn.SetBinding(nameof(Button.iconImage), new DataBinding()
				{
					dataSourcePath = new PropertyPath(nameof(ContextActionData.InputIcon)),
					bindingMode = BindingMode.ToTarget,
				});
				//image.SetBinding("source", new DataBinding()
				//{
				//	dataSourcePath = new PropertyPath(nameof(ContextActionData.inputIcon)),
				//	bindingMode = BindingMode.ToTarget,
				//	dataSource = _data.ContextActions[index]
				//});
			};
		}

		private void ApplyContext()
		{
			if (uiDocument == null || uiDocument.rootVisualElement == null)
			{
				return;
			}

			var context = uiDocument.rootVisualElement.Q<VisualElement>(contextPathName);

			var label = context.Q<Label>(contextTitlePathName);
			label.text = _data.Title.GetLocalizedString();

			var listView = context.Q<ListView>(contextListViewPathName);

			listView.bindItem = (element, index) =>
			{
				var btn = element.Q<Button>();
				var contextAction = _data.ContextActions[index];
				btn.text = contextAction.Verb.GetLocalizedString();
				btn.iconImage = contextAction.InputIcon;

			};
		}
	}


}
