using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;

namespace UMCP.Editor
{
    /// <summary>
    /// Utility class to have trustworthy periodic updates in the Unity Editor. (unlike DelayCall or EditorApplication.update which triggers at arbitrary intervals)
    /// </summary>
    [InitializeOnLoad]
    public static class EditorDelayCallService
    {
        /// <summary>
        /// Stack with actions to be executed on the main thread during editor updates.
        /// </summary>
        public static Stack<Action> actions = new Stack<Action>();

        /// <summary>
        /// Stopwatch to measure elapsed time during updates (so we don't exceed maxExecutionTimePerInvocation, and unnecassarily lag performance).
        /// </summary>
        private static System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        /// <summary>
        /// Timer to force periodic updates at a maximum interval, even if EditorApplication.update is not called frequently enough.
        /// </summary>
        private static System.Timers.Timer forceUpdateTimer = new System.Timers.Timer();

        /// <summary>
        /// Lock object to ensure thread-safe access to the actions stack.
        /// </summary>
        private static object lockObject = new object();

        /// <summary>
        /// Maximum interval between forced updates to ensure periodic execution. Reset after each update.
        /// </summary>
        private static double maxforceUpdateInterval = 1000; // ms

        /// <summary>
        /// Maximum execution time per invocation to prevent long blocking operations during editor updates.
        /// </summary>
        private static long maxExecutionTimePerInvocation = 3; // ms

        /// <summary>
        /// Enqueue an action to be executed on the main thread during the next editor update.
        /// </summary>
        /// <param name="action"></param>
        public static void Enqueue(Action action)
        {
            lock (lockObject)
            {
                actions.Push(action);
            }
        }

        /// <summary>
        /// Constructor to initialize the periodic update utility. Recalled automatically by Unity due to the InitializeOnLoad attribute.
        /// </summary>
        static EditorDelayCallService()
        {
            EditorApplication.update += OnEditorUpdate;
            stopwatch = new System.Diagnostics.Stopwatch();
            forceUpdateTimer .Interval = maxforceUpdateInterval; // 1 second interval
            forceUpdateTimer .Elapsed += (s, e) => 
            {
                OnEditorUpdate();
            };
            
            forceUpdateTimer.Start();
        }

        /// <summary>
        /// OnEditorUpdate is called periodically by Unity's EditorApplication.update and also forced by the timer to ensure regular execution.
        /// </summary>
        private static void OnEditorUpdate()
        {
            try
            {
                if (Monitor.TryEnter(lockObject))
                    {
                    stopwatch.Restart();
                    while (stopwatch.ElapsedMilliseconds < maxExecutionTimePerInvocation && actions.Count > 0)
                    {
                        Action action = actions.Pop();
                        action?.Invoke();
                    }
                    stopwatch.Stop();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (Monitor.IsEntered(lockObject))
                {
                    Monitor.Exit(lockObject);
                }
            }

            //Reset the timer to ensure periodic, but not premature, updates
            forceUpdateTimer.Stop();
            forceUpdateTimer.Start();
        }
    }
}
