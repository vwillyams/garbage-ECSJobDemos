using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Collections;
using Unity.Jobs;
using UnityEngine.Assertions;

namespace RotatorSamples
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