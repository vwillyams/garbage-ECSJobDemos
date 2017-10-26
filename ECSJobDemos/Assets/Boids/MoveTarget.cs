using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveTarget : MonoBehaviour {

    public float minSpeed1 = 10;
    public float maxSpeed1 = 30;
    public float frequency1 = 0.2f;

    public float minSpeed2 = 13;
    public float maxSpeed2 = 39;
    public float frequency2 = 0.3f;

    public float minSpeed3 = 17;
    public float maxSpeed3 = 42;
    public float frequency3 = 0.42f;

    // Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update ()
    {
        float modulation1 = (Mathf.Sin(Time.time * Mathf.PI * 2 * frequency1) + 1) * 0.5f;
        gameObject.transform.RotateAround(Vector3.up*10, Vector3.right, (modulation1 * (maxSpeed1 - minSpeed1) + minSpeed1) * Time.deltaTime);
        float modulation2 = (Mathf.Sin(Time.time * Mathf.PI * 2 * frequency2) + 1) * 0.5f;
		gameObject.transform.RotateAround(Vector3.up * 10, Vector3.forward, (modulation2 * (maxSpeed2 - minSpeed2) + minSpeed2) * Time.deltaTime);
        float modulation3 = (Mathf.Sin(Time.time * Mathf.PI * 2 * frequency3) + 1) * 0.5f;
		gameObject.transform.RotateAround(Vector3.up * 10, Vector3.up, (modulation3 * (maxSpeed3 - minSpeed3) + minSpeed3) * Time.deltaTime);
    }
}
