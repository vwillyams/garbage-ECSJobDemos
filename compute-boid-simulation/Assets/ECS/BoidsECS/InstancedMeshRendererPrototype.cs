using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace ECS
{
    public class InstancedMeshRendererPrototype : ScriptBehaviour
    {
		public static InstancedMeshRendererPrototype Instance;


		protected override void OnEnable ()
		{
			base.OnEnable ();
			Instance = this;
		}

		protected override void OnDisable ()
		{
			base.OnDisable ();
			if (Instance == this)
				Instance = null;
		}
    }
}