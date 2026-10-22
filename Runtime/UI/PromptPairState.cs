using UnityEngine.UIElements;

namespace TW.Interaction.Prompts
{
	public struct PromptPairState
	{
		public VisualElement rootUI;
		public VisualElement contentUI;
		public InteractPromptBinder binder;
		public bool isObstructed;
	}
}
