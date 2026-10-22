using TW.CullingGroups;
using UnityEngine;

namespace TW.Interaction.Prompts
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(InteractableObject))]
	[RequireComponent(typeof(CullingGroupObject))]
	public class InteractPromptBinder : MonoBehaviour
	{
		private InteractableObject _interactableObject;
		private CullingGroupObject _cullingGroupObject;
		public InteractableObject InteractableObject => _interactableObject;
		public CullingGroupObject CullingGroupObject => _cullingGroupObject;
		private void Awake()
		{
			TryGetComponent(out _cullingGroupObject);
			TryGetComponent(out _interactableObject);
		}

	}
}
