using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TW.Interaction.Prompts
{
	static class RegisterDisplayConverter
	{
#if UNITY_EDITOR
		[InitializeOnLoadMethod]
#endif
		[RuntimeInitializeOnLoadMethod]
		public static void Register()
		{
			// Register global converters here
			ConverterGroups.RegisterGlobalConverter((ref StyleEnum<DisplayStyle> handle) => handle.value == DisplayStyle.Flex);
			ConverterGroups.RegisterGlobalConverter((ref bool value) => new StyleEnum<DisplayStyle> { value = value ? DisplayStyle.Flex : DisplayStyle.None });
		}
	}

}