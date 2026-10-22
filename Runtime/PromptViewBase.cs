using UnityEngine;

namespace TW.Interaction.Prompts
{
	public abstract class PromptViewBase : MonoBehaviour
	{
		public abstract void SetObstructed(EntityId id, bool isObstructed);

		public abstract void SetTarget(EntityId id);


	}

}

