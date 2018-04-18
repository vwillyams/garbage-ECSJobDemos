using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    public float SceneSwitchInterval = 5.0f;

    public float TimeUntilNextSwitch = 0.0f;

    public int CurrentSceneIndex = -1;
	// Use this for initialization
	void Start ()
	{
	    DontDestroyOnLoad(this);
	    LoadNextScene();
	}

    private void LoadNextScene()
    {
        var sceneCount = SceneManager.sceneCountInBuildSettings;
        var nextIndex = CurrentSceneIndex + 1;
        if (nextIndex >= sceneCount)
            nextIndex = 0;
        if (SceneManager.GetSceneByBuildIndex(nextIndex).name == "SceneSwitcher")
            nextIndex++;

        Debug.Log(sceneCount);
        Debug.Log("Changing to scene: " + nextIndex);
        bool firstTime = CurrentSceneIndex == -1;
        TimeUntilNextSwitch = SceneSwitchInterval;
        CurrentSceneIndex = nextIndex;

        if (!firstTime)
        {
            World.Active.Dispose();
            DefaultWorldInitialization.Initialize(SceneManager.GetSceneByBuildIndex(nextIndex).name, false);
        }
        
        SceneManager.LoadScene(nextIndex);
        
        
    }

    // Update is called once per frame
	void Update ()
	{
	    TimeUntilNextSwitch -= Time.deltaTime;
	    if (TimeUntilNextSwitch > 0.0f)
	        return;
	    
	    LoadNextScene();
	}
}
