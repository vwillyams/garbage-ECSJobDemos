﻿using UnityEngine;

namespace Samples.Common
{
	public class RotatorOldUpdate : MonoBehaviour
	{
		[SerializeField]
		float 					m_Speed;

		public float speed
		{
			get { return m_Speed; }
			set { m_Speed = value; }
		}

		void Update ()
		{
			transform.rotation = transform.rotation * Quaternion.AngleAxis (m_Speed * Time.deltaTime, Vector3.up);
		}
	}
}