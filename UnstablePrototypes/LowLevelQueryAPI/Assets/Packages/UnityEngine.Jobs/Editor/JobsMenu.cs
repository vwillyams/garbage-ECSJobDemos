using UnityEditor;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;

class JobsMenu
{
    const string kDebuggerMenu = "Jobs/JobsDebugger";

    [MenuItem(kDebuggerMenu, false)]
    static void SwitchJobsDebugger()
    {
        JobsUtility.SetJobDebuggerEnabled(!JobsUtility.GetJobDebuggerEnabled());
    }

    [MenuItem(kDebuggerMenu, true)]
    static bool SwitchJobsDebuggerValidate()
    {
        Menu.SetChecked(kDebuggerMenu, JobsUtility.GetJobDebuggerEnabled());

        return true;
    }


    const string kLeakDetection = "Jobs/Leak Detection (Native Containers)";
    [MenuItem(kLeakDetection, false)]
    static void SwitchLeaks()
    {
        NativeLeakDetection.Mode = NativeLeakDetection.Mode == NativeLeakDetectionMode.Enabled ? NativeLeakDetectionMode.Disabled : NativeLeakDetectionMode.Enabled;
    }

    [MenuItem(kLeakDetection, true)]
    static bool SwitchLeaksValidate()
    {
        Menu.SetChecked(kLeakDetection, NativeLeakDetection.Mode == NativeLeakDetectionMode.Enabled);
        return true;
    }

    const string kBurst = "Jobs/Enable Burst Compiling";
    [MenuItem(kBurst, false)]
    static void EnableBurstCompiler()
    {
        UnityEditor.EditorPrefs.SetBool("ENABLE_BURST_COMPILER", !UnityEditor.EditorPrefs.GetBool("ENABLE_BURST_COMPILER"));
    }

    [MenuItem(kBurst, true)]
    static bool EnableBurstCompilerValidate()
    {
        Menu.SetChecked(kBurst, UnityEditor.EditorPrefs.GetBool("ENABLE_BURST_COMPILER"));
#if ENABLE_HLVM_COMPILER
        return true;
#else
        return false;
#endif
    }

    const string kBurstJobs = "Jobs/Use Burst Jobs";
    [MenuItem(kBurstJobs, false)]
    static void SwitchBurst()
    {
        JobsUtility.SetAllowUsingJobCompiler(!JobsUtility.GetAllowUsingJobCompiler());
    }

    [MenuItem(kBurstJobs, true)]
    static bool SwitchBurstValidate()
    {
        Menu.SetChecked(kBurstJobs, JobsUtility.GetAllowUsingJobCompiler());
        return UnityEditor.EditorPrefs.GetBool("ENABLE_BURST_COMPILER");
    }

}