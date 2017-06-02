using Internal.Runtime.Augments;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
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

        private static bool s_waitThreadStarted = false;

        private static LowLevelMonitor s_waitThreadStartedMonitor = new LowLevelMonitor();

        private static LowLevelMonitor s_activeWaitMonitor = new LowLevelMonitor();

        private static void WaitThreadStart()
        {
            s_waitThreadStartedMonitor.Acquire();
            s_waitThreadStarted = true;
            s_waitThreadStartedMonitor.Signal_Release();

            while(true)
            {
                s_activeWaitMonitor.Acquire();
                while(s_numActiveWaits == 0)
                {
                    s_activeWaitMonitor.Wait();
                }
                    
                int timeout = Timeout.Infinite;
                WaitHandle[] waitHandles = new WaitHandle[s_numActiveWaits];
                for (int i = 0; i < s_numActiveWaits; i++)
                {
                    RegisteredWaitHandle registeredHandle = s_registeredWaitHandles[i];
                    if(timeout == Timeout.Infinite)
                    {
                        timeout = registeredHandle.Timeout;
                    }
                    else
                    {
                        timeout = Math.Min(timeout, registeredHandle.Timeout); 
                    }
                    waitHandles[i] = registeredHandle.Handle;
                }
                s_activeWaitMonitor.Release();
                int signalledHandle = WaitHandle.WaitAny(waitHandles, timeout);

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
                        if (registeredHandle.Handle == waitHandles[signalledHandle])
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
                        if (registeredHandle.Timeout == timeout)
                        {
                            ExecuteWaitCompletion(registeredHandle, true);
                        }
                    }
                }
                s_activeWaitMonitor.Release();
            }
        }

        private static void ExecuteWaitCompletion(RegisteredWaitHandle registeredHandle, bool timedOut)
        {
            // TODO: Check for blocking case (InternalCompletionEvent in CoreCLR)
            ThreadPool.QueueUserWorkItem(CompleteWait, new CompletedWaitHandle(registeredHandle, timedOut));
        }

        private static void CompleteWait(object state)
        {
            CompletedWaitHandle handle = (CompletedWaitHandle)state;
            _ThreadPoolWaitOrTimerCallback.PerformWaitOrTimerCallback(handle.CompletedHandle.Callback, handle.TimedOut);
            if (!handle.CompletedHandle.Repeating)
            {
                UnregisterWait(handle.CompletedHandle);
            }
        }

        public static void RegisterWaitHandle(RegisteredWaitHandle handle)
        {
            StartWaitThreadIfNotStarted();

            s_activeWaitMonitor.Acquire();
            s_registeredWaitHandles[s_numActiveWaits++] = handle;
            s_activeWaitMonitor.Signal_Release();
        }

        private static void StartWaitThreadIfNotStarted()
        {
            s_waitThreadStartedMonitor.Acquire();
            if(!s_waitThreadStarted)
            {
                RuntimeThread waitThread = RuntimeThread.Create(WaitThreadStart);
                waitThread.IsBackground = true;
                waitThread.Start();
                while(!s_waitThreadStarted)
                {
                    s_waitThreadStartedMonitor.Wait();
                }
            }
            s_waitThreadStartedMonitor.Release();
        }

        public static void QueueUnregisterWait(RegisteredWaitHandle handle)
        {
            ThreadPool.QueueUserWorkItem(UnregisterWait, handle);
            UnregisterWait(handle);
        }

        private static void UnregisterWait(object state)
        {
            RegisteredWaitHandle handle = (RegisteredWaitHandle)state;
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
            Debug.Assert(handleIndex != -1);
            Array.Copy(s_registeredWaitHandles, handleIndex + 1, s_registeredWaitHandles, handleIndex, WaitHandle.MaxWaitHandles - (handleIndex + 1));
            s_registeredWaitHandles[s_numActiveWaits--] = null;
            s_activeWaitMonitor.Release();

            WaitHandle unregisterWaitHandle = handle.UserUnregisterWaitHandle;
            if (unregisterWaitHandle != null)
            {
                WaitSubsystem.SetEvent(unregisterWaitHandle.SafeWaitHandle.DangerousGetHandle());
            }
        }
    }
}
