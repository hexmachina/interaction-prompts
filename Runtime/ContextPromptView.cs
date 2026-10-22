using UnityEngine;
using UnityEngine.UIElements;

namespace TW.Interaction.Prompts
{
	public class ContextPromptView : MonoBehaviour
	{
		[SerializeField] private UIDocument uiDocument;
		[SerializeField] private ContextPromptData _data;
		[Header("Context Element Paths")]
		[SerializeField] private string contextPathName = "context";
		[SerializeField] private string contextTitlePathName = "title";
		[SerializeField] private string contextListViewPathName = "listView";

		private VisualElement m_Context;


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
