﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.ECS
{
	[ExecuteInEditMode]
	internal class PlayerLoopDisableManager : MonoBehaviour
	{
		public void OnEnable()
		{
			if (isActive)
			{
				isActive = false;
				DestroyImmediate(this.gameObject);
			}
		}
		public void OnDisable()
		{
			if (isActive)
				PlayerLoopManager.InvokeBeforeDomainUnload();
		}
		public bool isActive = false;
	}
}