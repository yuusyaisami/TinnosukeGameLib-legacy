// Game.Common.MainThread.cs
// Helper to detect main thread and assert usage for main-thread-only services.
// Behaves consistently in all build targets: accessing from the wrong thread always throws.

using System;
using System.Threading;
using UnityEngine;
namespace Game.Common
{
    public static class MainThread
    {
        static int s_mainThreadId;
        static bool s_initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void BindFromSubsystemRegistration()
        {
            CaptureMainThread("SubsystemRegistration");
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void BindEditor()
        {
            Bind("Editor");
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BindRuntime()
        {
            Bind("Runtime");
        }

        static void Bind(string source)
        {
            CaptureMainThread(source);
        }

        static void CaptureMainThread(string source)
        {
            var id = Thread.CurrentThread.ManagedThreadId;

            if (!s_initialized)
            {
                s_mainThreadId = id;
                s_initialized = true;
                return;
            }

            if (id != s_mainThreadId)
            {
                throw new InvalidOperationException($"[MainThread] {source} attempted to bind thread {id}, but main thread already captured as {s_mainThreadId}.");
            }
        }

        /// <summary>
        /// Ensures the current call happens on the Unity main thread.
        /// Throws InvalidOperationException when misused (all build targets).
        /// </summary>
        public static void AssertMainThread()
        {
            EnsureInitialized();
            if (Thread.CurrentThread.ManagedThreadId != s_mainThreadId)
                throw new InvalidOperationException(
                    $"Must be called on main thread. Current thread: {Thread.CurrentThread.ManagedThreadId}, Main: {s_mainThreadId}");
        }

        static void EnsureInitialized()
        {
            if (s_initialized)
                return;
            throw new InvalidOperationException("MainThread not bound yet. SubsystemRegistration should have already captured the main thread before any usage.");
        }

        /// <summary>
        /// Returns true if on main thread. In Editor/Dev, throws if MainThread is not initialized yet.
        /// </summary>
        public static bool IsMainThread
        {
            get
            {
                EnsureInitialized();
                return Thread.CurrentThread.ManagedThreadId == s_mainThreadId;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Initialize MainThread for unit tests. Editor-only.
        /// </summary>
        public static void InitializeForTests()
        {
            s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
            s_initialized = true;
        }
#endif
    }
}
