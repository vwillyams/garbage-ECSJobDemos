#if ENABLE_HLVM_COMPILER

using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using System.IO;
using p2gc;
#endif

namespace Unity.Jobs
{
    public enum Support
    {
        Strict,
        Relaxed
    }

    public enum Accuracy
    {
        Low,
        Med,
        High,
        Std
    }

    public class ComputeJobOptimizationAttribute : System.Attribute
    {
#if ENABLE_HLVM_COMPILER
        internal p2gc.JitOptions m_Options;
#endif
        public ComputeJobOptimizationAttribute()
        {
#if ENABLE_HLVM_COMPILER
            m_Options = new JitOptions();
#endif
        }

        public ComputeJobOptimizationAttribute(Accuracy accuracy, Support support)
        {
#if ENABLE_HLVM_COMPILER

            p2gc.Support p2gcSupport = p2gc.Support.VM_STRICT;
            switch (support)
            { 
                case Support.Strict:
                    p2gcSupport = p2gc.Support.VM_STRICT;
                    break;
                case Support.Relaxed:
                    p2gcSupport = p2gc.Support.VM_RELAXED;
                    break;
            }

            p2gc.Accuracy p2gcAccuracy = p2gc.Accuracy.VM_STDP;
            switch (accuracy)
            {
                case Accuracy.Low:
                    p2gcAccuracy = p2gc.Accuracy.VM_LOWP;
                    break;
                case Accuracy.Med:
                    p2gcAccuracy = p2gc.Accuracy.VM_MEDP;
                    break;
                case Accuracy.High:
                    p2gcAccuracy = p2gc.Accuracy.VM_HIGHP;
                    break;
            }

            //@TODO: Optimization level 5 crashes boid demo
            m_Options = new JitOptions(VMLibrary.GetNativeTarget(false), 0, p2gcAccuracy, p2gcSupport);
#endif
        }
    }
}

#if ENABLE_HLVM_COMPILER


namespace Unity.Jobs
{
[UnityEditor.InitializeOnLoad]
class HLVMCompilationEngine
{
    //@TODO: Investigate if this has additional unnecessary bindings / function indirection
    public static void AddICalls(List<JitExternalFunction> functions)
    {
        AddIcall(typeof(UnsafeUtility),        "MemCpy", functions);
        AddIcall(typeof(UnsafeUtility),        "Malloc", functions);
        AddIcall(typeof(UnsafeUtility),        "Free", functions);
//      AddIcall(typeof(JobsUtility),          "PatchBufferMinMaxRanges", functions);
    }

    public static void AddIcall(Type type, string methodName, List<JitExternalFunction> functions)
    {
        var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (method == null)
            throw new System.ArgumentException("ICall does not exist: " + methodName);

        IntPtr func = method.MethodHandle.GetFunctionPointer();

        string externalName = type.FullName + "_" + method.Name;
        foreach (var arg in method.GetParameters())
            externalName += "_" + arg.ParameterType.FullName;
        externalName = externalName.Replace(".", "__");
        // Debug.Log(externalName);

        functions.Add(new JitExternalFunction(externalName, func));
    }

    public static JitExternalFunction[] GenerateExternalCalls()
    { 
        List<JitExternalFunction> externalBindings = new List<JitExternalFunction>();

        var externalNames = UnityEngineInternal.Jobs.JobCompiler.GetExternalCallNames();
        var externalsPtrs = UnityEngineInternal.Jobs.JobCompiler.GetExternalCallPtrs();
        for (int i = 0; i != externalNames.Length; i++)
            externalBindings.Add(new JitExternalFunction(externalNames[i], externalsPtrs[i]));
        
        AddICalls(externalBindings);

        return externalBindings.ToArray();
    }


    static System.IntPtr Compile(System.Type jobType, System.Object function)
    {
        // Debug.Log("Compiling " + assemblyPath + " -> " + fullStructName);

        p2gc.JitOptions options = null;
        object[] attributes = jobType.GetCustomAttributes(typeof(ComputeJobOptimizationAttribute), false);
        if (attributes == null || attributes.Length != 1)
        {
            return IntPtr.Zero;
        }
        else
        {
            ComputeJobOptimizationAttribute attrib = (ComputeJobOptimizationAttribute)attributes[0];
            options = attrib.m_Options;
        }

        string fullStructName = jobType.FullName;

        string key = jobType.FullName + "_" + ((System.Delegate)function).Method.Name;
        System.IntPtr cache = UnityEngineInternal.Jobs.JobCompiler.GetCachedJobFunction(key);
        if (cache != IntPtr.Zero)
        {
            return cache;
        }

        string unityEngineDLLSearchDir = Path.GetDirectoryName(typeof(UnityEngine.Object).Assembly.Location);
        string[] searchPaths = { unityEngineDLLSearchDir };

        System.Delegate callbackFunction = (System.Delegate)function;

        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        
        p2gc.JitCompiler compiler = new p2gc.JitCompiler (searchPaths);
        options.DumpFlags = options.DumpFlags | JitDumpFlags.LLVM;
        var result = compiler.CompileMethod(callbackFunction.Method, options, GenerateExternalCalls());
        
        stopwatch.Stop();

        if (result.FunctionPointer != System.IntPtr.Zero)
        {
            UnityEngineInternal.Jobs.JobCompiler.SetCachedJobFunction(key, result.FunctionPointer);
            Debug.Log(string.Format("Compiled : {0} in {1} seconds", fullStructName, stopwatch.Elapsed.Seconds));
        }
        else
            Debug.Log("Failed Compilation: " + fullStructName);


        return result.FunctionPointer;
    }

    static HLVMCompilationEngine()
    {
        if (UnityEditor.EditorPrefs.GetBool("ENABLE_BURST_COMPILER"))
        {
             Debug.Log("Initialize Burst Compiler");
             UnityEngineInternal.Jobs.JobCompiler.JitCompile += Compile;
        }
    }
}


class InvalidateBurstJobCache : UnityEditor.AssetPostprocessor
{
    public static void Invalidate(string[] importedAssets)
    {
        const string ext = ".cs";

        foreach (string str in importedAssets)
        {
            if (str.EndsWith(ext))
            {
                UnityEngineInternal.Jobs.JobCompiler.ClearCachedJobFunctions();
                return;
            }
        }

    }
    
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        Invalidate(importedAssets);
        Invalidate(movedAssets);
        Invalidate(deletedAssets);
    }
}


#endif

