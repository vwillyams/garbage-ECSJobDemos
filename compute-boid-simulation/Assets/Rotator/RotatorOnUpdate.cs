using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using UnityEngine.Assertions;

public class RotatorOnUpdate : ScriptBehaviour
{
	[SerializeField]
	float 					m_Speed;

	public float speed
	{
		get { return m_Speed; }
		set { m_Speed = value; }
	}

	protected override void OnUpdate ()
	{
		base.OnUpdate ();

		transform.rotation = transform.rotation * Quaternion.AngleAxis (m_Speed * Time.deltaTime, Vector3.up);
	}
}