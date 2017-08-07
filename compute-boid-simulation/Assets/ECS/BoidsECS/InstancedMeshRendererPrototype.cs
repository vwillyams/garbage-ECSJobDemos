using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.ECS;

namespace BoidSimulations
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