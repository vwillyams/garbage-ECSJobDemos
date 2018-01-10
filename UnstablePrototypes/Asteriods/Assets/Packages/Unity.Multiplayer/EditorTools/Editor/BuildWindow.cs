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

    void OnGUI()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Build Server"))
        {
            var serverDefine = "ASTEROIDS_SERVER";
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defines + ";" + serverDefine);

            BuildOptions options = BuildOptions.Development | BuildOptions.AllowDebugging;

            BuildTools.BuildGame(StandardBuildPath + "/Server", "Server.exe", BuildTarget.StandaloneWindows64, options, "ServerBuild");
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defines);
        }
        if (GUILayout.Button("Build Client"))
        {
            var clientDefine = "ASTEROIDS_CLIENT";
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defines + ";" + clientDefine);

            BuildOptions options = BuildOptions.Development | BuildOptions.AllowDebugging;

            BuildTools.BuildGame(StandardBuildPath + "/Client", "Client.exe", BuildTarget.StandaloneWindows64, options, "ClientBuild");
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defines);
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
    }

    static readonly string StandardBuildPath = "StandardBuild";
}
#endif