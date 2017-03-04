// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Internal.Runtime.Augments
{
    using Interop = global::Interop; /// due to the existence of <see cref="Internal.Interop"/>
    using OSThreadPriority = Interop.mincore.ThreadPriority;

    public sealed partial class RuntimeThread
    {
        [ThreadStatic]
        private static int t_reentrantWaitSuppressionCount;

        [ThreadStatic]
        private static ApartmentType t_apartmentType;

        private object _threadStartArg;
        private SafeWaitHandle _osHandle;

        /// <summary>
        /// Used by <see cref="WaitHandle"/>'s multi-wait functions
        /// </summary>
        private WaitHandleArray<IntPtr> _waitedHandles;

        private void PlatformSpecificInitialize()
        {
            _waitedHandles = new WaitHandleArray<IntPtr>(elementInitializer: null);
        }

        internal IntPtr[] GetWaitedHandleArray(int requiredCapacity)
        {
            Debug.Assert(this == CurrentThread);

            _waitedHandles.EnsureCapacity(requiredCapacity);
            return _waitedHandles.Items;
        }

        private void PlatformSpecificInitializeExistingThread()
        {
            _osHandle = GetOSHandleForCurrentThread();
            _priority = MapFromOSPriority(Interop.mincore.GetThreadPriority(_osHandle));
        }

        private static SafeWaitHandle GetOSHandleForCurrentThread()
        {
            IntPtr currentProcHandle = Interop.mincore.GetCurrentProcess();
            IntPtr currentThreadHandle = Interop.mincore.GetCurrentThread();
            SafeWaitHandle threadHandle;

            if (Interop.mincore.DuplicateHandle(currentProcHandle, currentThreadHandle, currentProcHandle,
                out threadHandle, 0, false, (uint)Interop.Constants.DuplicateSameAccess))
            {
                return threadHandle;
            }

            // Throw an ApplicationException for compatibility with CoreCLR. First save the error code.
            int errorCode = Marshal.GetLastWin32Error();
            var ex = new ApplicationException();
            ex.SetErrorCode(errorCode);
            throw ex;
        }

        private static ThreadPriority MapFromOSPriority(OSThreadPriority priority)
        {
            if (priority <= OSThreadPriority.Lowest)
            {
                // OS thread priorities in the [Idle,Lowest] range are mapped to ThreadPriority.Lowest
                return ThreadPriority.Lowest;
            }
            switch (priority)
            {
                case OSThreadPriority.BelowNormal:
                    return ThreadPriority.BelowNormal;

                case OSThreadPriority.Normal:
                    return ThreadPriority.Normal;

                case OSThreadPriority.AboveNormal:
                    return ThreadPriority.AboveNormal;

                case OSThreadPriority.ErrorReturn:
                    Debug.Fail("GetThreadPriority failed");
                    return ThreadPriority.Normal;
            }
            // Handle OSThreadPriority.ErrorReturn value before this check!
            if (priority >= OSThreadPriority.Highest)
            {
                // OS thread priorities in the [Highest,TimeCritical] range are mapped to ThreadPriority.Highest
                return ThreadPriority.Highest;
            }
            Debug.Fail("Unreachable");
            return ThreadPriority.Normal;
        }

        private static OSThreadPriority MapToOSPriority(ThreadPriority priority)
        {
            switch (priority)
            {
                case ThreadPriority.Lowest:
                    return OSThreadPriority.Lowest;

                case ThreadPriority.BelowNormal:
                    return OSThreadPriority.BelowNormal;

                case ThreadPriority.Normal:
                    return OSThreadPriority.Normal;

                case ThreadPriority.AboveNormal:
                    return OSThreadPriority.AboveNormal;

                case ThreadPriority.Highest:
                    return OSThreadPriority.Highest;

                default:
                    Debug.Fail("Unreachable");
                    return OSThreadPriority.Normal;
            }
        }

        private ThreadPriority GetPriority()
        {
            if (_osHandle.IsInvalid)
            {
                // The thread has not been started yet; return the value assigned to the Priority property.
                // Race condition with setting the priority or starting the thread is OK, we may return an old value.
                return _priority;
            }

            // The priority might have been changed by external means. Obtain the actual value from the OS
            // rather than using the value saved in _priority.
            OSThreadPriority osPriority = Interop.mincore.GetThreadPriority(_osHandle);
            return MapFromOSPriority(osPriority);
        }

        private bool SetPriority(ThreadPriority priority)
        {
            if (_osHandle.IsInvalid)
            {
                Debug.Assert(GetThreadStateBit(ThreadState.Unstarted));
                // We will set the priority (saved in _priority) when we create an OS thread
                return true;
            }
            return Interop.mincore.SetThreadPriority(_osHandle, (int)MapToOSPriority(priority));
        }

        /// <summary>
        /// Checks if the underlying OS thread has finished execution.
        /// </summary>
        /// <remarks>
        /// Use this method only on started threads and threads being started in StartCore.
        /// </remarks>
        private bool HasFinishedExecution()
        {
            // If an external thread dies and its Thread object is resurrected, _osHandle will be finalized, i.e. invalid
            if (_osHandle.IsInvalid)
            {
                return true;
            }
            uint result = Interop.mincore.WaitForSingleObject(_osHandle, dwMilliseconds: 0);
            return result == (uint)Interop.Constants.WaitObject0;
        }

        private bool JoinCore(int millisecondsTimeout)
        {
            SafeWaitHandle waitHandle = _osHandle;
            int result;

            waitHandle.DangerousAddRef();
            try
            {
                result = WaitHandle.WaitForSingleObject(waitHandle.DangerousGetHandle(), millisecondsTimeout);
            }
            finally
            {
                waitHandle.DangerousRelease();
            }

            return result == (int)Interop.Constants.WaitObject0;
        }

        private void StartCore(object parameter)
        {
            using (LockHolder.Hold(_lock))
            {
                if (!GetThreadStateBit(ThreadState.Unstarted))
                {
                    throw new ThreadStateException(SR.ThreadState_AlreadyStarted);
                }

                const int AllocationGranularity = (int)0x10000; // 64k

                int stackSize = _maxStackSize;
                if ((0 < stackSize) && (stackSize < AllocationGranularity))
                {
                    // If StackSizeParamIsAReservation flag is set and the reserve size specified by CreateThread's
                    // dwStackSize parameter is less than or equal to the initially committed stack size specified in
                    // the executable header, the reserve size will be set to the initially committed size rounded up
                    // to the nearest multiple of 1 MiB. In all cases the reserve size is rounded up to the nearest
                    // multiple of the system's allocation granularity (typically 64 KiB).
                    //
                    // To prevent overreservation of stack memory for small stackSize values, we increase stackSize to
                    // the allocation granularity. We assume that the SizeOfStackCommit field of IMAGE_OPTIONAL_HEADER
                    // is strictly smaller than the allocation granularity (the field's default value is 4 KiB);
                    // otherwise, at least 1 MiB of memory will be reserved. Note that the desktop CLR increases
                    // stackSize to 256 KiB if it is smaller than that.
                    stackSize = AllocationGranularity;
                }

                bool waitingForThreadStart = false;
                GCHandle threadHandle = GCHandle.Alloc(this);
                _threadStartArg = parameter;
                uint threadId;

                try
                {
                    _osHandle = Interop.mincore.CreateThread(IntPtr.Zero, (IntPtr)stackSize,
                        AddrofIntrinsics.AddrOf<Interop.mincore.ThreadProc>(StartThread), (IntPtr)threadHandle,
                        (uint)(Interop.Constants.CreateSuspended | Interop.Constants.StackSizeParamIsAReservation),
                        out threadId);

                    // CoreCLR ignores OS errors while setting the priority, so do we
                    SetPriority(_priority);

                    Interop.mincore.ResumeThread(_osHandle);

                    // Skip cleanup if any asynchronous exception happens while waiting for the thread start
                    waitingForThreadStart = true;

                    // Wait until the new thread either dies or reports itself as started
                    while (GetThreadStateBit(ThreadState.Unstarted) && !HasFinishedExecution())
                    {
                        Yield();
                    }

                    waitingForThreadStart = false;
                }
                finally
                {
                    Debug.Assert(!waitingForThreadStart, "Leaked threadHandle");
                    if (!waitingForThreadStart)
                    {
                        threadHandle.Free();
                        _threadStartArg = null;
                    }
                }

                if (GetThreadStateBit(ThreadState.Unstarted))
                {
                    // Lack of memory is the only expected reason for thread creation failure
                    throw new ThreadStartException(new OutOfMemoryException());
                }
            }
        }

        [NativeCallable(CallingConvention = CallingConvention.StdCall)]
        private static uint StartThread(IntPtr parameter)
        {
            GCHandle threadHandle = (GCHandle)parameter;
            RuntimeThread thread = (RuntimeThread)threadHandle.Target;
            Delegate threadStart = thread._threadStart;
            // Get the value before clearing the ThreadState.Unstarted bit
            object threadStartArg = thread._threadStartArg;

            try
            {
                t_currentThread = thread;
                System.Threading.ManagedThreadId.SetForCurrentThread(thread._managedThreadId);
            }
            catch (OutOfMemoryException)
            {
                // Terminate the current thread. The creator thread will throw a ThreadStartException.
                return 0;
            }

            // Report success to the creator thread, which will free threadHandle and _threadStartArg
            thread.ClearThreadStateBit(ThreadState.Unstarted);

            try
            {
                // The Thread cannot be started more than once, so we may clean up the delegate
                thread._threadStart = null;

#if ENABLE_WINRT
                // If this call fails, COM and WinRT calls on this thread will fail with CO_E_NOTINITIALIZED.
                // We may continue and fail on the actual call.
                Interop.WinRT.RoInitialize(Interop.WinRT.RO_INIT_TYPE.RO_INIT_MULTITHREADED);
#endif

                ParameterizedThreadStart paramThreadStart = threadStart as ParameterizedThreadStart;
                if (paramThreadStart != null)
                {
                    paramThreadStart(threadStartArg);
                }
                else
                {
                    ((ThreadStart)threadStart)();
                }
            }
            finally
            {
                thread.SetThreadStateBit(ThreadState.Stopped);
            }
            return 0;
        }

        public ApartmentState GetApartmentState() { throw null; }
        public bool TrySetApartmentState(ApartmentState state) { throw null; }
        public void DisableComObjectEagerCleanup() { throw null; }
        public void Interrupt() { throw null; }

        internal static void UninterruptibleSleep0()
        {
            Interop.mincore.Sleep(0);
        }

        private static void SleepCore(int millisecondsTimeout)
        {
            Debug.Assert(millisecondsTimeout >= -1);
            Interop.mincore.Sleep((uint)millisecondsTimeout);
        }

        //
        // Suppresses reentrant waits on the current thread, until a matching call to RestoreReentrantWaits.
        // This should be used by code that's expected to be called inside the STA message pump, so that it won't 
        // reenter itself.  In an ASTA, this should only be the CCW implementations of IUnknown and IInspectable.
        //
        internal static void SuppressReentrantWaits()
        {
            t_reentrantWaitSuppressionCount++;
        }

        internal static void RestoreReentrantWaits()
        {
            Debug.Assert(t_reentrantWaitSuppressionCount > 0);
            t_reentrantWaitSuppressionCount--;
        }

        internal static bool ReentrantWaitsEnabled =>
            GetCurrentApartmentType() == ApartmentType.STA && t_reentrantWaitSuppressionCount == 0;

        internal static ApartmentType GetCurrentApartmentType()
        {
            ApartmentType currentThreadType = t_apartmentType;
            if (currentThreadType != ApartmentType.Unknown)
                return currentThreadType;

            Interop._APTTYPE aptType;
            Interop._APTTYPEQUALIFIER aptTypeQualifier;
            int result = Interop.mincore.CoGetApartmentType(out aptType, out aptTypeQualifier);

            ApartmentType type = ApartmentType.Unknown;

            switch ((Interop.Constants)result)
            {
                case Interop.Constants.CoENotInitialized:
                    type = ApartmentType.None;
                    break;

                case Interop.Constants.SOk:
                    switch (aptType)
                    {
                        case Interop._APTTYPE.APTTYPE_STA:
                        case Interop._APTTYPE.APTTYPE_MAINSTA:
                            type = ApartmentType.STA;
                            break;

                        case Interop._APTTYPE.APTTYPE_MTA:
                            type = ApartmentType.MTA;
                            break;

                        case Interop._APTTYPE.APTTYPE_NA:
                            switch (aptTypeQualifier)
                            {
                                case Interop._APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_MTA:
                                case Interop._APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_IMPLICIT_MTA:
                                    type = ApartmentType.MTA;
                                    break;

                                case Interop._APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_STA:
                                case Interop._APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_MAINSTA:
                                    type = ApartmentType.STA;
                                    break;

                                default:
                                    Debug.Assert(false, "NA apartment without NA qualifier");
                                    break;
                            }
                            break;
                    }
                    break;

                default:
                    Debug.Assert(false, "bad return from CoGetApartmentType");
                    break;
            }

            if (type != ApartmentType.Unknown)
                t_apartmentType = type;
            return type;
        }

        internal enum ApartmentType
        {
            Unknown = 0,
            None,
            STA,
            MTA
        }
    }
}
