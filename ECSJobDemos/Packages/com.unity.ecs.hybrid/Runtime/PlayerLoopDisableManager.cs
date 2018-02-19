using UnityEngine;

namespace Unity.ECS.Hybrid
{
	[ExecuteInEditMode]
	class PlayerLoopDisableManager : MonoBehaviour
	{
	    public bool IsActive;

	    public void OnEnable()
		{
		    if (!IsActive)
		        return;

		    IsActive = false;
		    DestroyImmediate(gameObject);
		}

	    public void OnDisable()
		{
			if (IsActive)
				PlayerLoopManager.InvokeBeforeDomainUnload();
		}
	}
}
