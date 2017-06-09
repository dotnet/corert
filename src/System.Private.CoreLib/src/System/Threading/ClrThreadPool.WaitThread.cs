// Licensed to the .NET Foundation under one or more agreements.
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

            private static readonly RegisteredWaitHandle[] s_registeredWaitHandles = new RegisteredWaitHandle[WaitHandle.MaxWaitHandles - 1];
            private static readonly WaitHandle[] s_waitHandles = new WaitHandle[WaitHandle.MaxWaitHandles];
            private static int s_numUserWaits = 0;
            private static int s_currentTimeout = Timeout.Infinite;
            private static readonly LowLevelLock s_registeredHandlesLock = new LowLevelLock();

            private static readonly RegisteredWaitHandle[] s_pendingRemoves = new RegisteredWaitHandle[WaitHandle.MaxWaitHandles - 1]; // Lock
            private static int s_numPendingRemoves = 0;
            private static readonly LowLevelLock s_removesLock = new LowLevelLock();

            private static readonly AutoResetEvent s_changeHandlesEvent = new AutoResetEvent(false);

            private static readonly AutoResetEvent s_safeToDisposeHandleEvent = new AutoResetEvent(false);

            private static bool s_waitThreadStarted = false;
            private static LowLevelLock s_waitThreadStartedLock = new LowLevelLock();

            private static void WaitThreadStart()
            {
                while (true)
                {
                    s_safeToDisposeHandleEvent.Reset();
                    ProcessRemovals();
                    int signaledHandleIndex = WaitHandle.WaitAny(s_waitHandles, s_numUserWaits + 1, s_currentTimeout);
                    WaitHandle signaledHandle = s_waitHandles[signaledHandleIndex];
                    ProcessRemovals();
                    s_safeToDisposeHandleEvent.Set();

                    // Indices may have changed when processing removals and the signalled handle may have already been unregistered
                    // so we do a linear search over the active user waits to see if the signaled handle is still registered
                    if (signaledHandleIndex != WaitHandle.WaitTimeout)
                    {
                        for (int i = 0; i < s_numUserWaits; i++)
                        {
                            RegisteredWaitHandle registeredHandle = s_registeredWaitHandles[i];
                            if (registeredHandle.Handle == signaledHandle)
                            {
                                ExecuteWaitCompletion(registeredHandle, false);
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < s_numUserWaits; i++)
                        {
                            RegisteredWaitHandle registeredHandle = s_registeredWaitHandles[i];
                            if (registeredHandle.Timeout == s_currentTimeout)
                            {
                                ExecuteWaitCompletion(registeredHandle, true);
                            }
                        }
                    }
                }
            }

            private static void ProcessRemovals()
            {
                s_removesLock.Acquire();
                s_registeredHandlesLock.Acquire();
                if(s_numPendingRemoves == 0)
                {
                    s_registeredHandlesLock.Release();
                    s_removesLock.Release();
                    return;
                }

                // This is O(N^2), but max(N) = 63 and N will usually be very low
                for (int i = 0; i < s_numPendingRemoves; i++)
                {
                    for (int j = 0; j < s_numUserWaits; j++)
                    {
                        if (s_pendingRemoves[i] == s_registeredWaitHandles[j])
                        {
                            s_registeredWaitHandles[j] = null;
                            s_waitHandles[j + 1] = null;
                            break;
                        }
                    }
                    s_pendingRemoves[i] = null;
                }
                s_numPendingRemoves = 0;

                // Fill in nulls
                // This is O(1), Goes through each of the 63 possible handles once.
                for (int i = 0; i < s_numUserWaits; i++)
                {
                    if (s_registeredWaitHandles[i] == null)
                    {
                        for (int j = s_numUserWaits - 1; j > i; j--)
                        {
                            if(s_registeredWaitHandles[j] != null)
                            {
                                s_registeredWaitHandles[i] = s_registeredWaitHandles[j];
                                s_registeredWaitHandles[j] = null;
                                s_numUserWaits = j;
                                break;
                            }
                        }
                        if (s_registeredWaitHandles[i] == null)
                        {
                            s_numUserWaits = i - 1;
                            break;
                        }
                    }
                }

                // Recalculate Timeout
                int timeout = Timeout.Infinite;
                for (int i = 0; i < s_numUserWaits; i++)
                {
                    if (timeout == Timeout.Infinite)
                    {
                        timeout = s_registeredWaitHandles[i].Timeout;
                    }
                    else
                    {
                        timeout = Math.Min(s_registeredWaitHandles[i].Timeout, timeout);
                    }
                }
                s_currentTimeout = timeout;
                s_registeredHandlesLock.Release();
                s_removesLock.Release();
            }

            private static void ExecuteWaitCompletion(RegisteredWaitHandle registeredHandle, bool timedOut)
            {
                ThreadPool.QueueUserWorkItem(CompleteWait, new CompletedWaitHandle(registeredHandle, timedOut));
            }

            private static void CompleteWait(object state)
            {
                CompletedWaitHandle handle = (CompletedWaitHandle)state;
                handle.CompletedHandle.CanUnregister.Reset();
                handle.CompletedHandle.PerformCallback(handle.TimedOut);
                handle.CompletedHandle.CanUnregister.Set();
                if (!handle.CompletedHandle.Repeating)
                {
                    QueueUnregisterWait(handle.CompletedHandle);
                }
            }

            public static bool RegisterWaitHandle(RegisteredWaitHandle handle)
            {
                StartWaitThreadIfNotStarted();

                if (s_numUserWaits == WaitHandle.MaxWaitHandles - 1)
                {
                    return false;
                }

                AddWaitHandleForNextWait(handle);

                s_changeHandlesEvent.Set();
                return true;
            }

            private static void AddWaitHandleForNextWait(RegisteredWaitHandle handle)
            {
                s_registeredHandlesLock.Acquire();

                s_registeredWaitHandles[s_numUserWaits] = handle;
                s_waitHandles[s_numUserWaits + 1] = handle.Handle;
                s_numUserWaits++;
                if (s_currentTimeout == Timeout.Infinite)
                {
                    s_currentTimeout = handle.Timeout;
                }
                else
                {
                    s_currentTimeout = Math.Min(s_currentTimeout, handle.Timeout);
                }

                s_registeredHandlesLock.Release();
            }

            private static void StartWaitThreadIfNotStarted()
            {
                s_waitThreadStartedLock.Acquire();
                if (!s_waitThreadStarted)
                {
                    s_waitHandles[0] = s_changeHandlesEvent;
                    RuntimeThread waitThread = RuntimeThread.Create(WaitThreadStart);
                    waitThread.IsBackground = true;
                    waitThread.Start();
                }
                s_waitThreadStartedLock.Release();
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
                s_safeToDisposeHandleEvent.WaitOne();
            }

            private static void UnregisterWait(object state)
            {
                RegisteredWaitHandle handle = (RegisteredWaitHandle)state;
                
                // TODO: Optimization: Try to unregister wait directly if it isn't being waited on.
                s_removesLock.Acquire();
                s_pendingRemoves[s_numPendingRemoves++] = handle;
                s_removesLock.Release();
                s_changeHandlesEvent.Set();
                if (handle.UserUnregisterWaitHandle != null)
                {
                    WaitHandle.WaitAll(new WaitHandle[] { s_safeToDisposeHandleEvent, handle.CanUnregister });

                    if (handle.UserUnregisterWaitHandle.SafeWaitHandle.DangerousGetHandle() != (IntPtr)(-1))
                    {
                        handle.SignalUserWaitHandle();
                    }
                }
            }
        }
    }
}
