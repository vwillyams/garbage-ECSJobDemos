using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Assertions.Comparers;

namespace Unity
{
    public class Debug
    {
//        internal static ILogger s_Logger = (ILogger)new Logger((ILogHandler)new DebugLogHandler());
//
//        public static ILogger unityLogger
//        {
//            get { return Debug.s_Logger; }
//        }
//
//        [ExcludeFromDocs]
//        public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration)
//        {
//            bool depthTest = true;
//            Debug.DrawLine(start, end, color, duration, depthTest);
//        }
//
//        [ExcludeFromDocs]
//        public static void DrawLine(Vector3 start, Vector3 end, Color color)
//        {
//            bool depthTest = true;
//            float duration = 0.0f;
//            Debug.DrawLine(start, end, color, duration, depthTest);
//        }
//
//        [ExcludeFromDocs]
//        public static void DrawLine(Vector3 start, Vector3 end)
//        {
//            bool depthTest = true;
//            float duration = 0.0f;
//            Color white = Color.white;
//            Debug.DrawLine(start, end, white, duration, depthTest);
//        }
//
//        public static void DrawLine(Vector3 start, Vector3 end, [DefaultValue("Color.white")] Color color, [DefaultValue("0.0f")] float duration, [DefaultValue("true")] bool depthTest)
//        {
//            Debug.DrawLine_Injected(ref start, ref end, ref color, duration, depthTest);
//        }
//
//        [ExcludeFromDocs]
//        public static void DrawRay(Vector3 start, Vector3 dir, Color color, float duration)
//        {
//            bool depthTest = true;
//            Debug.DrawRay(start, dir, color, duration, depthTest);
//        }
//
//        [ExcludeFromDocs]
//        public static void DrawRay(Vector3 start, Vector3 dir, Color color)
//        {
//            bool depthTest = true;
//            float duration = 0.0f;
//            Debug.DrawRay(start, dir, color, duration, depthTest);
//        }
//
//        [ExcludeFromDocs]
//        public static void DrawRay(Vector3 start, Vector3 dir)
//        {
//            bool depthTest = true;
//            float duration = 0.0f;
//            Color white = Color.white;
//            Debug.DrawRay(start, dir, white, duration, depthTest);
//        }
//
//        public static void DrawRay(Vector3 start, Vector3 dir, [DefaultValue("Color.white")] Color color, [DefaultValue("0.0f")] float duration, [DefaultValue("true")] bool depthTest)
//        {
//            Debug.DrawLine(start, start + dir, color, duration, depthTest);
//        }
//
//        [MethodImpl(MethodImplOptions.InternalCall)]
//        public static extern void Break();
//
//        [MethodImpl(MethodImplOptions.InternalCall)]
//        public static extern void DebugBreak();
//
//        public static void Log(object message)
//        {
//            Debug.unityLogger.Log(LogType.Log, message);
//        }
//
//        public static void Log(object message, Object context)
//        {
//            Debug.unityLogger.Log(LogType.Log, message, context);
//        }
//
//        public static void LogFormat(string format, params object[] args)
//        {
//            Debug.unityLogger.LogFormat(LogType.Log, format, args);
//        }
//
//        public static void LogFormat(Object context, string format, params object[] args)
//        {
//            Debug.unityLogger.LogFormat(LogType.Log, context, format, args);
//        }
//
        public static void LogError(object message)
        {
            UnityEngine.Debug.LogError(message);
        }
//
//        public static void LogError(object message, Object context)
//        {
//            Debug.unityLogger.Log(LogType.Error, message, context);
//        }
//
//        public static void LogErrorFormat(string format, params object[] args)
//        {
//            Debug.unityLogger.LogFormat(LogType.Error, format, args);
//        }
//
//        public static void LogErrorFormat(Object context, string format, params object[] args)
//        {
//            Debug.unityLogger.LogFormat(LogType.Error, context, format, args);
//        }
//
//        [MethodImpl(MethodImplOptions.InternalCall)]
//        public static extern void ClearDeveloperConsole();
//
//        public static extern bool developerConsoleVisible
//        {
//            [MethodImpl(MethodImplOptions.InternalCall)]
//            get;
//            [MethodImpl(MethodImplOptions.InternalCall)]
//            set;
//        }
//
        public static void LogException(Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
        }
//
//        public static void LogException(Exception exception, Object context)
//        {
//            Debug.unityLogger.LogException(exception, context);
//        }
//
//        [MethodImpl(MethodImplOptions.InternalCall)]
//        internal static extern void LogPlayerBuildError(string message, string file, int line, int column);
//
//        public static void LogWarning(object message)
//        {
//            Debug.unityLogger.Log(LogType.Warning, message);
//        }
//
//        public static void LogWarning(object message, Object context)
//        {
//            Debug.unityLogger.Log(LogType.Warning, message, context);
//        }
//
//        public static void LogWarningFormat(string format, params object[] args)
//        {
//            Debug.unityLogger.LogFormat(LogType.Warning, format, args);
//        }
//
//        public static void LogWarningFormat(Object context, string format, params object[] args)
//        {
//            Debug.unityLogger.LogFormat(LogType.Warning, context, format, args);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void Assert(bool condition)
//        {
//            if (condition)
//                return;
//            Debug.unityLogger.Log(LogType.Assert, (object)"Assertion failed");
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void Assert(bool condition, Object context)
//        {
//            if (condition)
//                return;
//            Debug.unityLogger.Log(LogType.Assert, (object)"Assertion failed", context);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void Assert(bool condition, object message)
//        {
//            if (condition)
//                return;
//            Debug.unityLogger.Log(LogType.Assert, message);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void Assert(bool condition, string message)
//        {
//            if (condition)
//                return;
//            Debug.unityLogger.Log(LogType.Assert, (object)message);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void Assert(bool condition, object message, Object context)
//        {
//            if (condition)
//                return;
//            Debug.unityLogger.Log(LogType.Assert, message, context);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void Assert(bool condition, string message, Object context)
//        {
//            if (condition)
//                return;
//            Debug.unityLogger.Log(LogType.Assert, (object)message, context);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AssertFormat(bool condition, string format, params object[] args)
//        {
//            if (condition)
//                return;
//            Debug.unityLogger.LogFormat(LogType.Assert, format, args);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AssertFormat(bool condition, Object context, string format, params object[] args)
//        {
//            if (condition)
//                return;
//            Debug.unityLogger.LogFormat(LogType.Assert, context, format, args);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void LogAssertion(object message)
//        {
//            Debug.unityLogger.Log(LogType.Assert, message);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void LogAssertion(object message, Object context)
//        {
//            Debug.unityLogger.Log(LogType.Assert, message, context);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void LogAssertionFormat(string format, params object[] args)
//        {
//            Debug.unityLogger.LogFormat(LogType.Assert, format, args);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void LogAssertionFormat(Object context, string format, params object[] args)
//        {
//            Debug.unityLogger.LogFormat(LogType.Assert, context, format, args);
//        }
//
//        public static extern bool isDebugBuild
//        {
//            [MethodImpl(MethodImplOptions.InternalCall)]
//            get;
//        }
//
//        [MethodImpl(MethodImplOptions.InternalCall)]
//        internal static extern void OpenConsoleFile();
//
//        [MethodImpl(MethodImplOptions.InternalCall)]
//        internal static extern void GetDiagnosticSwitches(List<DiagnosticSwitch> results);
//
//        [MethodImpl(MethodImplOptions.InternalCall)]
//        internal static extern object GetDiagnosticSwitch(string name);
//
//        [MethodImpl(MethodImplOptions.InternalCall)]
//        internal static extern void SetDiagnosticSwitch(string name, object value, bool setPersistent);
//
//        [Obsolete("Assert(bool, string, params object[]) is obsolete. Use AssertFormat(bool, string, params object[]) (UnityUpgradable) -> AssertFormat(*)", true)]
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void Assert(bool condition, string format, params object[] args)
//        {
//            if (condition)
//                return;
//            Debug.unityLogger.LogFormat(LogType.Assert, format, args);
//        }
//
//        [Obsolete("Debug.logger is obsolete. Please use Debug.unityLogger instead (UnityUpgradable) -> unityLogger")]
//        [EditorBrowsable(EditorBrowsableState.Never)]
//        public static ILogger logger
//        {
//            get { return Debug.s_Logger; }
//        }
//
//        [MethodImpl(MethodImplOptions.InternalCall)]
//        private static extern void DrawLine_Injected(ref Vector3 start, ref Vector3 end, [DefaultValue("Color.white")] ref Color color, [DefaultValue("0.0f")] float duration, [DefaultValue("true")] bool depthTest);
    }
}

namespace Unity.Assertions
{
    [DebuggerStepThrough]
    public static class Assert
    {
        [Conditional("UNITY_ASSERTIONS")]
        public static void IsTrue(bool condition)
        {
           UnityEngine.Assertions.Assert.IsTrue(condition);
        }

        [Conditional("UNITY_ASSERTIONS")]
        public static void IsTrue(bool condition, string message)
        {
            UnityEngine.Assertions.Assert.IsTrue(condition, message);
        }

        [Conditional("UNITY_ASSERTIONS")]
        public static void IsFalse(bool condition)
        {
            UnityEngine.Assertions.Assert.IsFalse(condition);
        }

        [Conditional("UNITY_ASSERTIONS")]
        public static void IsFalse(bool condition, string message)
        {
            UnityEngine.Assertions.Assert.IsFalse(condition, message);
        }

        [Conditional("UNITY_ASSERTIONS")]
        public static void AreApproximatelyEqual(float expected, float actual)
        {
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(expected, actual);
        }

        [Conditional("UNITY_ASSERTIONS")]
        public static void AreApproximatelyEqual(float expected, float actual, string message)
        {
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(expected, actual, message);
        }

        [Conditional("UNITY_ASSERTIONS")]
        public static void AreApproximatelyEqual(float expected, float actual, float tolerance)
        {
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(expected, actual, tolerance);
        }

//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreApproximatelyEqual(float expected, float actual, float tolerance, string message)
//        {
//            Assert.AreEqual<float>(expected, actual, message, (IEqualityComparer<float>) new FloatComparer(tolerance));
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreNotApproximatelyEqual(float expected, float actual)
//        {
//            Assert.AreNotEqual<float>(expected, actual, (string) null,
//                (IEqualityComparer<float>) FloatComparer.s_ComparerWithDefaultTolerance);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreNotApproximatelyEqual(float expected, float actual, string message)
//        {
//            Assert.AreNotEqual<float>(expected, actual, message,
//                (IEqualityComparer<float>) FloatComparer.s_ComparerWithDefaultTolerance);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreNotApproximatelyEqual(float expected, float actual, float tolerance)
//        {
//            Assert.AreNotApproximatelyEqual(expected, actual, tolerance, (string) null);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreNotApproximatelyEqual(float expected, float actual, float tolerance, string message)
//        {
//            Assert.AreNotEqual<float>(expected, actual, message,
//                (IEqualityComparer<float>) new FloatComparer(tolerance));
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreEqual<T>(T expected, T actual)
//        {
//            Assert.AreEqual<T>(expected, actual, (string) null);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreEqual<T>(T expected, T actual, string message)
//        {
//            Assert.AreEqual<T>(expected, actual, message, (IEqualityComparer<T>) EqualityComparer<T>.Default);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreEqual<T>(T expected, T actual, string message, IEqualityComparer<T> comparer)
//        {
//            if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
//            {
//                Assert.AreEqual((object) expected as UnityEngine.Object, (object) actual as UnityEngine.Object,
//                    message);
//            }
//            else
//            {
//                if (comparer.Equals(actual, expected))
//                    return;
//                Assert.Fail(AssertionMessageUtil.GetEqualityMessage((object) actual, (object) expected, true), message);
//            }
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreEqual(UnityEngine.Object expected, UnityEngine.Object actual, string message)
//        {
//            if (!(actual != expected))
//                return;
//            Assert.Fail(AssertionMessageUtil.GetEqualityMessage((object) actual, (object) expected, true), message);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreNotEqual<T>(T expected, T actual)
//        {
//            Assert.AreNotEqual<T>(expected, actual, (string) null);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreNotEqual<T>(T expected, T actual, string message)
//        {
//            Assert.AreNotEqual<T>(expected, actual, message, (IEqualityComparer<T>) EqualityComparer<T>.Default);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreNotEqual<T>(T expected, T actual, string message, IEqualityComparer<T> comparer)
//        {
//            if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
//            {
//                Assert.AreNotEqual((object) expected as UnityEngine.Object, (object) actual as UnityEngine.Object,
//                    message);
//            }
//            else
//            {
//                if (!comparer.Equals(actual, expected))
//                    return;
//                Assert.Fail(AssertionMessageUtil.GetEqualityMessage((object) actual, (object) expected, false),
//                    message);
//            }
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void AreNotEqual(UnityEngine.Object expected, UnityEngine.Object actual, string message)
//        {
//            if (!(actual == expected))
//                return;
//            Assert.Fail(AssertionMessageUtil.GetEqualityMessage((object) actual, (object) expected, false), message);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void IsNull<T>(T value) where T : class
//        {
//            Assert.IsNull<T>(value, (string) null);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void IsNull<T>(T value, string message) where T : class
//        {
//            if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
//            {
//                Assert.IsNull((object) value as UnityEngine.Object, message);
//            }
//            else
//            {
//                if ((object) value == null)
//                    return;
//                Assert.Fail(AssertionMessageUtil.NullFailureMessage((object) value, true), message);
//            }
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void IsNull(UnityEngine.Object value, string message)
//        {
//            if (!(value != (UnityEngine.Object) null))
//                return;
//            Assert.Fail(AssertionMessageUtil.NullFailureMessage((object) value, true), message);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void IsNotNull<T>(T value) where T : class
//        {
//            Assert.IsNotNull<T>(value, (string) null);
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void IsNotNull<T>(T value, string message) where T : class
//        {
//            if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
//            {
//                Assert.IsNotNull((object) value as UnityEngine.Object, message);
//            }
//            else
//            {
//                if ((object) value != null)
//                    return;
//                Assert.Fail(AssertionMessageUtil.NullFailureMessage((object) value, false), message);
//            }
//        }
//
//        [Conditional("UNITY_ASSERTIONS")]
//        public static void IsNotNull(UnityEngine.Object value, string message)
//        {
//            if (!(value == (UnityEngine.Object) null))
//                return;
//            Assert.Fail(AssertionMessageUtil.NullFailureMessage((object) value, false), message);
//        }

    }
}
