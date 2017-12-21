using System;
using System.Text;
using System.IO;
using UnityEditor;
using Unity.Jobs;

#if UNITY_EDITOR
namespace Unity.Burst.LowLevel
{
    [InitializeOnLoad]
    internal class BurstLoader
    {
        static BurstLoader()
        {
            // Un-comment the following to log compilation steps to log.txt in the .Runtime folder
            // Environment.SetEnvironmentVariable("UNITY_BURST_DEBUG", "1");

            // Try to load the runtime through an environment variable
            var runtimePath = Environment.GetEnvironmentVariable("UNITY_BURST_RUNTIME_PATH");

            // Otherwise try to load it from the package itself
            if (!Directory.Exists(runtimePath))
            {
                runtimePath = Path.GetFullPath("Packages/com.unity.burst/.Runtime");
            }
            BurstCompilerService.Initialize(runtimePath, ExtractBurstCompilerOptions);
        }

        const string kEnableSafetyChecksPref = "BurstSafetyChecks";
        const string kEnableSafetyChecks = "Jobs/Enable Burst Safety Checks";

        [MenuItem(kEnableSafetyChecks, false)]
        static void EnableBurstSafetyChecks()
        {
            EditorPrefs.SetBool(kEnableSafetyChecksPref, !EditorPrefs.GetBool(kEnableSafetyChecksPref, true));
        }

        [MenuItem(kEnableSafetyChecks, true)]
        static bool EnableBurstSafetyChecksValidate()
        {
            Menu.SetChecked(kEnableSafetyChecks, EditorPrefs.GetBool(kEnableSafetyChecksPref, true));
            return BurstCompilerService.IsInitialized;
        }

        public static bool ExtractBurstCompilerOptions(Type type, out string optimizationFlags)
        {
            bool shouldBurstCompile = false;

            foreach (var attr in type.GetCustomAttributes(true))
            {
                // Use resolution by name instead to avoid tighly coupled components
                if (attr.GetType().FullName == "Unity.Jobs.ComputeJobOptimizationAttribute")
                {
                    shouldBurstCompile = true;
                    break;
                }
            }

            optimizationFlags = null;

            if (!shouldBurstCompile)
            {
                return false;
            }

            var builder = new StringBuilder();


            if (!EditorPrefs.GetBool(kEnableSafetyChecksPref))
                AddOption(builder, "-disable-safety-checks");

            //Debug.Log($"ExtractBurstCompilerOptions: {type} {optimizationFlags}");

            // AddOption(builder, "-enable-module-caching-debugger");
            // AddOption(builder, "-cache-directory=Library/BurstCache");
            
            optimizationFlags = builder.ToString();

            return true;
        }

        static void AddOption(StringBuilder builder, string option)
        {
            if (builder.Length != 0)
                builder.Append(' ');

            builder.Append(option);
        }
    }
}

#endif