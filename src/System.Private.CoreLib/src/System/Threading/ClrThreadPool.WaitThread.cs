﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        public static class WaitThread
        {
            private struct CompletedWaitHandle
            {
                public CompletedWaitHandle(RegisteredWaitHandle completedHandle, bool timedOut)
                {
                    CompletedHandle = completedHandle;
                    TimedOut = timedOut;
                }

                public RegisteredWaitHandle CompletedHandle { get; }
                public bool TimedOut { get; }
            }

            private static readonly RegisteredWaitHandle[] s_registeredWaitHandles = new RegisteredWaitHandle[WaitHandle.MaxWaitHandles];

            private static int s_numActiveWaits = 0;

            private static WaitHandle[] s_waitHandles = new WaitHandle[0];

            private static int s_currentTimeout = Timeout.Infinite;

            private static bool s_waitThreadStarted = false;

            private static LowLevelMonitor s_waitThreadStartedMonitor = new LowLevelMonitor();

            private static LowLevelMonitor s_activeWaitMonitor = new LowLevelMonitor();

            private static void WaitThreadStart()
            {
                s_waitThreadStartedMonitor.Acquire();
                s_waitThreadStarted = true;
                s_waitThreadStartedMonitor.Signal_Release();

                while (true)
                {
                    s_activeWaitMonitor.Acquire();
                    while (s_numActiveWaits == 0)
                    {
                        s_activeWaitMonitor.Wait();
                    }
                    s_activeWaitMonitor.Release();
                    int signalledHandle = WaitHandle.WaitAny(s_waitHandles, s_currentTimeout);

                    s_activeWaitMonitor.Acquire();
                    if (s_numActiveWaits == 0)
                    {
                        s_activeWaitMonitor.Release();
                        continue;
                    }

                    if (signalledHandle != WaitHandle.WaitTimeout)
                    {
                        for (int i = 0; i < s_numActiveWaits; i++)
                        {
                            RegisteredWaitHandle registeredHandle = s_registeredWaitHandles[i];
                            if (registeredHandle.Handle == s_waitHandles[signalledHandle])
                            {
                                ExecuteWaitCompletion(registeredHandle, false);
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < s_numActiveWaits; i++)
                        {
                            RegisteredWaitHandle registeredHandle = s_registeredWaitHandles[i];
                            if (registeredHandle.Timeout == s_currentTimeout)
                            {
                                ExecuteWaitCompletion(registeredHandle, true);
                            }
                        }
                    }
                    s_activeWaitMonitor.Release();
                }
            }

            private static void UpdateWaitHandlesAndTimeout()
            {
                s_currentTimeout = Timeout.Infinite;
                s_waitHandles = new WaitHandle[s_numActiveWaits];
                for (int i = 0; i < s_numActiveWaits; i++)
                {
                    RegisteredWaitHandle registeredHandle = s_registeredWaitHandles[i];
                    if (s_currentTimeout == Timeout.Infinite)
                    {
                        s_currentTimeout = registeredHandle.Timeout;
                    }
                    else
                    {
                        s_currentTimeout = Math.Min(s_currentTimeout, registeredHandle.Timeout);
                    }
                    s_waitHandles[i] = registeredHandle.Handle;
                }
            }

            private static void ExecuteWaitCompletion(RegisteredWaitHandle registeredHandle, bool timedOut)
            {
                ThreadPool.QueueUserWorkItem(CompleteWait, new CompletedWaitHandle(registeredHandle, timedOut));
            }

            private static void CompleteWait(object state)
            {
                CompletedWaitHandle handle = (CompletedWaitHandle)state;
                handle.CompletedHandle.CanUnregister.Reset();
                _ThreadPoolWaitOrTimerCallback.PerformWaitOrTimerCallback(handle.CompletedHandle.Callback, handle.TimedOut);
                handle.CompletedHandle.CanUnregister.Set();
                if (!handle.CompletedHandle.Repeating)
                {
                    QueueUnregisterWait(handle.CompletedHandle);
                }
            }

            public static void RegisterWaitHandle(RegisteredWaitHandle handle)
            {
                StartWaitThreadIfNotStarted();

                s_activeWaitMonitor.Acquire();
                s_registeredWaitHandles[s_numActiveWaits++] = handle;

                UpdateWaitHandlesAndTimeout();
                s_activeWaitMonitor.Signal_Release();
            }

            private static void StartWaitThreadIfNotStarted()
            {
                s_waitThreadStartedMonitor.Acquire();
                if (!s_waitThreadStarted)
                {
                    RuntimeThread waitThread = RuntimeThread.Create(WaitThreadStart);
                    waitThread.IsBackground = true;
                    waitThread.Start();
                    while (!s_waitThreadStarted)
                    {
                        s_waitThreadStartedMonitor.Wait();
                    }
                }
                s_waitThreadStartedMonitor.Release();
            }

            public static void QueueUnregisterWait(RegisteredWaitHandle handle)
            {
                if (handle.Handle?.SafeWaitHandle.DangerousGetHandle() == (IntPtr)(-1))
                {
                    UnregisterWait(handle);
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(UnregisterWait, handle);
                }
            }

            private static void UnregisterWait(object state)
            {
                RegisteredWaitHandle handle = (RegisteredWaitHandle)state;
                handle.CanUnregister.Wait();
                s_activeWaitMonitor.Acquire();
                int handleIndex = -1;
                for (int i = 0; i < s_numActiveWaits; i++)
                {
                    if (s_registeredWaitHandles[i] == handle)
                    {
                        handleIndex = i;
                        break;
                    }
                }
                if (handleIndex != -1)
                {
                    Array.Copy(s_registeredWaitHandles, handleIndex + 1, s_registeredWaitHandles, handleIndex, WaitHandle.MaxWaitHandles - (handleIndex + 1));
                    s_registeredWaitHandles[s_numActiveWaits--] = null;
                }

                UpdateWaitHandlesAndTimeout();
                s_activeWaitMonitor.Release();
                handle.SignalUserWaitHandle();
            }
        }
    }
}
