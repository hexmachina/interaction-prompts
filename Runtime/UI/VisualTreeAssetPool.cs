using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;

public class VisualTreeAssetPool : MonoBehaviour
{
	[SerializeField] VisualTreeAsset m_EntryItemAsset;
	[SerializeField] private int initialPoolSize = 5;
	[SerializeField] private int maxPoolSize = 8;
	[SerializeField] private int inUse = 0;
	public bool collectionChecks = true;

	IObjectPool<TemplateContainer> m_Pool;

	public IObjectPool<TemplateContainer> Pool
	{
		get
		{
			m_Pool ??= new ObjectPool<TemplateContainer>(CreatePrompt, OnTakeFromPool, OnReturnedToPool, OnDestroyPoolObject, collectionChecks, initialPoolSize, maxPoolSize);
			return m_Pool;
		}
	}


	private TemplateContainer CreatePrompt()
	{
		var icon = m_EntryItemAsset.Instantiate();
		icon.style.display = DisplayStyle.None;
		return icon;
	}

	void OnTakeFromPool(TemplateContainer obj)
	{
		inUse++;
		//obj.RegisterCallback<TransitionEndEvent>(TransitionEnd);
	}

	private void TransitionEnd(TransitionEndEvent evt)
	{
		Debug.Log("here");
	}
	public void Release(VisualElement obj)
	{

		Pool.Release((TemplateContainer)obj);

	}
	private void OnReturnedToPool(TemplateContainer obj)
	{
		inUse--;
		obj.style.display = DisplayStyle.None;
		obj.RemoveFromHierarchy();

	}

	void OnDestroyPoolObject(TemplateContainer obj)
	{
		obj.RemoveFromHierarchy();
		obj = null;
	}

}
