#if UNITY_EDITOR

using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

class BuildTools
{
    public static UnityEditor.Build.Reporting.BuildReport BuildGame(string buildPath, string exeName, BuildTarget target, BuildOptions opts, string buildId)
    {
        var levels = new string[]
        {
            "Assets/level0.unity"
        };

        var exePathName = buildPath + "/" + exeName;
        Debug.Log("Building: " + exePathName);
        Directory.CreateDirectory(buildPath);

        // Set all files to be writeable (As Unity 2017.1 sets them to read only)
        string fullBuildPath = Directory.GetCurrentDirectory() + "/" + buildPath;
        string[]fileNames = Directory.GetFiles(fullBuildPath);
        foreach(var fileName in fileNames)
        {
            FileAttributes attributes = File.GetAttributes(fileName);
            attributes &= ~FileAttributes.ReadOnly;
            File.SetAttributes(fileName, attributes);
        }

        Environment.SetEnvironmentVariable("BUILD_ID", buildId, EnvironmentVariableTarget.Process);
        var result = BuildPipeline.BuildPlayer(levels, exePathName, target, opts);
        Environment.SetEnvironmentVariable("BUILD_ID", "", EnvironmentVariableTarget.Process);

        return result;
    }
}
public class BuildWindow : EditorWindow
{
    [MenuItem("Multiplayer/Windows/Build Tools")]
    public static void ShowWindow()
    {
        GetWindow<BuildWindow>(false, "Build Tools", true);
    }

    enum BuildType
    {
        None,
        Default,
        Client,
        Server,
        All
    }

    void OnGUI()
    {
        var target = BuildType.None;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Build Server"))
        {
            target = BuildType.Server;
        }
        if (GUILayout.Button("Build Client"))
        {
            target = BuildType.Client;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Build All Targets"))
        {
            target = BuildType.All;
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Open Build Folder"))
        {
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo = new System.Diagnostics.ProcessStartInfo("explorer.exe", StandardBuildPath);
            p.Start();

            Debug.Log(PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone));
        }
        GUILayout.EndHorizontal();

        if (target == BuildType.None)
            return;
        else if (target == BuildType.All)
        {
            BuildForTarget("server");
            BuildForTarget("client");
        }
        else
        {
            string name = (target == BuildType.Server ? "server" : "client");
            BuildForTarget(name);
        }
    }

    void BuildForTarget(string name)
    {
        // TODO: Build target selection UI
        BuildTarget target = BuildTarget.StandaloneWindows;
        Debug.Log("Current Platform: " + Application.platform);

        if(Application.platform == RuntimePlatform.OSXEditor)
            target = BuildTarget.StandaloneOSX;

        Debug.Log("Target: " + target);

        try
        {
            File.Copy(name + "_defines", "Assets/mcs.rsp");
            BuildOptions options = BuildOptions.Development | BuildOptions.AllowDebugging;
            BuildTools.BuildGame(StandardBuildPath + "/" + name, "_" + name + ".exe", target, options, name + "_build");
            File.Delete("Assets/mcs.rsp");
        }
        catch(Exception e)
        {
            Debug.LogException(e);
        }
    }

    static readonly string StandardBuildPath = "StandardBuild";
}
#endif
