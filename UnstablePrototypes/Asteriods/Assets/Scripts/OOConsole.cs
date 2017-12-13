using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.GameCode.DebugTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-1001)]
public class OOConsole : MonoBehaviour
{
    static OOConsole m_Instance;
    public static DebugOverlay DebugOverlay { get { return m_Instance.m_DebugOverlay; }}
    public static Console Console { get { return m_Instance.m_Console; }}
    DebugOverlay m_DebugOverlay;
    Console m_Console;

    void Awake()
    {
        Debug.Assert(m_Instance == null);
        m_Instance = this;
        DontDestroyOnLoad(gameObject);

        Init();

        wfeof = new WaitForEndOfFrame();
        StartCoroutine(EndOfFrame());
    }

    void Init()
    {
        m_DebugOverlay = new DebugOverlay();
        m_DebugOverlay.Init(120, 36);

        m_Console = new Console();
        m_Console.Init();

        m_Console.AddCommand("quit", CmdQuit, "Quit game");

        OOConsole.Console.Write("^FFFGame initialized^F44.^4F4.^44F.\n");
    }

    void OnDestroy()
    {
        Debug.Assert(m_Instance == this);

        m_DebugOverlay.Shutdown();
        m_DebugOverlay = null;
        m_Console.Shutdown();
        m_Console = null;

        m_Instance = null;
    }

    void Update()
    {
        OnUpdate();
        m_Console.TickUpdate();
    }

    void LateUpdate()
    {
        m_Console.TickLateUpdate();
        m_DebugOverlay.TickLateUpdate();
    }

    void EndOfFrameUpdate()
    {
        m_DebugOverlay.Render();
    }

    // Console and Overlay Update


    void OnUpdate()
    {
        DebugOverlay.Write(1, 0, "FPS:{0,6:###.##}", 1.0f / Time.deltaTime);
    }

    // Console Commands
    void CmdQuit(string[] args)
    {
        OOConsole.Console.Write("Goodbye\n");
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator EndOfFrame() { while(true) { yield return wfeof; EndOfFrameUpdate(); } }
    WaitForEndOfFrame wfeof;
}
