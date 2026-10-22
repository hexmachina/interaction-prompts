using System.Collections.Generic;
using TW.Interaction.Inputs;
using Unity.Properties;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;
[assembly: Unity.Properties.GeneratePropertyBagsForAssembly]

namespace TW.Interaction.Prompts
{

	[GeneratePropertyBag, CreateAssetMenu(menuName = "Data/Prompts/Context Prompt Data")]
	public partial class ContextPromptData : ScriptableObject
	{
		[SerializeField] private bool _engaged = false;
		[SerializeField] private LocalizedString _title = new();
		[SerializeField] private List<ContextActionData> _contextActions = new();
		[CreateProperty]
		public bool Engaged
		{
			get => _engaged; set
			{
				if (_engaged == value) return;
				_engaged = value;

			}
		}

		[CreateProperty]
		public LocalizedString Title
		{
			get => _title;
			set
			{
				if (value == _title)
				{
					return;
				}
				_title = value;

			}
		}
		[CreateProperty] public IList<ContextActionData> ContextActions => _contextActions;

		public void UpdateActivities(IList<InteractableActivity> activities, IList<InteractInput> inputs)
		{
			int count = Mathf.Max(_contextActions.Count, activities.Count);
			for (int i = 0; i < count; i++)
			{
				if (i >= inputs.Count)
				{
					break;
				}
				if (i >= _contextActions.Count)
				{
					_contextActions.Add(new ContextActionData()
					{
						InputIcon = inputs[i].imageIcon,
						Verb = activities[i].Verb
					});
				}
				else
				{
					_contextActions[i].InputIcon = inputs[i].imageIcon;
					_contextActions[i].Verb = activities[i].Verb;

				}
				if (i >= activities.Count)
				{
					_contextActions.RemoveAt(_contextActions.Count - 1);

				}
			}

		}

	}

	[System.Serializable, GeneratePropertyBag]
	public partial class ContextActionData
	{
		[SerializeField] private LocalizedString _verb;
		[SerializeField] private Background _inputIcon;
		[CreateProperty]
		public Background InputIcon
		{
			get => _inputIcon; set
			{
				if (_inputIcon == value) return;
				_inputIcon = value;

			}
		}
		private string _verbValue;

		[CreateProperty]
		public LocalizedString Verb
		{
			get => _verb;
			set
			{
				if (_verb != value)
				{
					_verb = value;
					_verbValue = GetVerbString();
				}
			}
		}
		[CreateProperty]
		public string VerbString
		{
			get
			{
				_verbValue ??= GetVerbString();
				return _verbValue;
			}
		}

		private string GetVerbString()
		{
			return Verb == null || Verb.TableReference.ReferenceType == UnityEngine.Localization.Tables.TableReference.Type.Empty
							? $"Missing Localization"
							: Verb.GetLocalizedString();
		}

	}
}
