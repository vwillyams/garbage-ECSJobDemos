using MonoBehaviour = UnityEngine.MonoBehaviour;
using ExecuteInEditMode = UnityEngine.ExecuteInEditMode;

namespace Unity.ECS
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
