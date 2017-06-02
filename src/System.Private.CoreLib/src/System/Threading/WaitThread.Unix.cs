using Internal.Runtime.Augments;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    public static class WaitThread
    {
        private static readonly RegisteredWaitHandle[] s_registeredWaitHandles = new RegisteredWaitHandle[WaitHandle.MaxWaitHandles];

        private static int s_numActiveWaits = 0;

        private static bool s_waitThreadStarted = false;

        private static LowLevelMonitor s_waitThreadStartedMonitor = new LowLevelMonitor();

        private static LowLevelMonitor s_activeWaitMonitor = new LowLevelMonitor();

        private static IntPtr WaitThreadStart(IntPtr context)
        {
            RuntimeThread.InitializeThreadPoolThread();

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
                            CompleteWait(registeredHandle, false);
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
                            CompleteWait(registeredHandle, true);
                        }
                    }
                }
                s_activeWaitMonitor.Release();
            }
        }

        private static void CompleteWait(RegisteredWaitHandle registeredHandle, bool timedOut)
        {
            _ThreadPoolWaitOrTimerCallback.PerformWaitOrTimerCallback(registeredHandle.Callback, timedOut);
            if (!registeredHandle.Repeating)
            {
                UnregisterWaitHandleDangerous(registeredHandle);
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
            if (!s_waitThreadStarted)
            {
                if (!Interop.Sys.RuntimeThread_CreateThread(IntPtr.Zero /*use default stack size*/,
                    AddrofIntrinsics.AddrOf<Interop.Sys.ThreadProc>(WaitThreadStart), IntPtr.Zero))
                {
                    throw new OutOfMemoryException();
                }

                while (!s_waitThreadStarted)
                {
                    s_waitThreadStartedMonitor.Wait();
                }
            }
            s_waitThreadStartedMonitor.Release();
        }

        public static void UnregisterWaitHandle(RegisteredWaitHandle handle, WaitHandle unregisterEvent)
        {
            s_activeWaitMonitor.Acquire();
            UnregisterWaitHandleDangerous(handle);
            if (unregisterEvent != null)
            {
                WaitSubsystem.SetEvent(unregisterEvent.SafeWaitHandle.DangerousGetHandle());
            }
            s_activeWaitMonitor.Release();
        }

        /// <summary>
        /// Unregister a registered wait handle. This method does not acquire or release the <see cref="s_activeWaitMonitor"/>. That needs to be done by the caller.
        /// </summary>
        /// <param name="handle">The registered wait handle to unregister.</param>
        private static void UnregisterWaitHandleDangerous(RegisteredWaitHandle handle)
        {
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
        }
    }
}
