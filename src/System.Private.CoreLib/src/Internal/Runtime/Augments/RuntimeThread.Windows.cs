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

        [ThreadStatic]
        private static bool t_comInitializedByUs;

        private SafeWaitHandle _osHandle;

        private ApartmentState _initialAppartmentState = ApartmentState.Unknown;

        /// <summary>
        /// Used by <see cref="WaitHandle"/>'s multi-wait functions
        /// </summary>
        private WaitHandleArray<IntPtr> _waitedHandles;

        private void PlatformSpecificInitialize()
        {
            _waitedHandles = new WaitHandleArray<IntPtr>(elementInitializer: null);
        }

        // Platform-specific initialization of foreign threads, i.e. threads not created by Thread.Start
        private void PlatformSpecificInitializeExistingThread()
        {
            _osHandle = GetOSHandleForCurrentThread();
        }

        /// <summary>
        /// Callers must ensure to clear and return the array after use
        /// </summary>
        internal SafeWaitHandle[] RentWaitedSafeWaitHandleArray(int requiredCapacity)
        {
            Debug.Assert(this == CurrentThread);

            if (_waitedSafeWaitHandles.Items == null)
            {
                return null;
            }

            _waitedSafeWaitHandles.VerifyElementsAreDefault();
            _waitedSafeWaitHandles.EnsureCapacity(requiredCapacity);
            return _waitedSafeWaitHandles.RentItems();
        }

        internal void ReturnWaitedSafeWaitHandleArray(SafeWaitHandle[] waitedSafeWaitHandles)
        {
            Debug.Assert(this == CurrentThread);
            _waitedSafeWaitHandles.ReturnItems(waitedSafeWaitHandles);
        }

        /// <summary>
        /// Callers must ensure to return the array after use
        /// </summary>
        internal IntPtr[] RentWaitedHandleArray(int requiredCapacity)
        {
            Debug.Assert(this == CurrentThread);

            if (_waitedHandles.Items == null)
            {
                return null;
            }

            _waitedHandles.EnsureCapacity(requiredCapacity);
            return _waitedHandles.RentItems();
        }

        internal void ReturnWaitedHandleArray(IntPtr[] waitedHandles)
        {
            Debug.Assert(this == CurrentThread);
            _waitedHandles.ReturnItems(waitedHandles);
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
            ex.HResult = errorCode;
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

        private ThreadPriority GetPriorityLive()
        {
            Debug.Assert(!_osHandle.IsInvalid);
            return MapFromOSPriority(Interop.mincore.GetThreadPriority(_osHandle));
        }

        private bool SetPriorityLive(ThreadPriority priority)
        {
            Debug.Assert(!_osHandle.IsInvalid);
            return Interop.mincore.SetThreadPriority(_osHandle, (int)MapToOSPriority(priority));
        }

        private ThreadState GetThreadState()
        {
            int state = _threadState;
            // If the thread is marked as alive, check if it has finished execution
            if ((state & (int)(ThreadState.Unstarted | ThreadState.Stopped | ThreadState.Aborted)) == 0)
            {
                if (JoinInternal(0))
                {
                    state = _threadState;
                    if ((state & (int)(ThreadState.Stopped | ThreadState.Aborted)) == 0)
                    {
                        SetThreadStateBit(ThreadState.Stopped);
                        state = _threadState;
                    }
                }
            }
            return (ThreadState)state;
        }

        private bool JoinInternal(int millisecondsTimeout)
        {
            // This method assumes the thread has been started
            Debug.Assert(!GetThreadStateBit(ThreadState.Unstarted) || (millisecondsTimeout == 0));
            SafeWaitHandle waitHandle = _osHandle;

            // If an OS thread is terminated and its Thread object is resurrected, _osHandle may be finalized and closed
            if (waitHandle.IsClosed)
            {
                return true;
            }

            // Handle race condition with the finalizer
            try
            {
                waitHandle.DangerousAddRef();
            }
            catch (ObjectDisposedException)
            {
                return true;
            }

            try
            {
                int result;

                if (millisecondsTimeout == 0)
                {
                    result = (int)Interop.mincore.WaitForSingleObject(waitHandle.DangerousGetHandle(), 0);
                }
                else
                {
                    result = WaitHandle.WaitForSingleObject(waitHandle.DangerousGetHandle(), millisecondsTimeout, true);
                }

                return result == (int)Interop.Constants.WaitObject0;
            }
            finally
            {
                waitHandle.DangerousRelease();
            }
        }

        private bool CreateThread(GCHandle thisThreadHandle)
        {
            const int AllocationGranularity = 0x10000;  // 64 KiB

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

            uint threadId;
            _osHandle = Interop.mincore.CreateThread(IntPtr.Zero, (IntPtr)stackSize,
                AddrofIntrinsics.AddrOf<Interop.mincore.ThreadProc>(ThreadEntryPoint), (IntPtr)thisThreadHandle,
                (uint)(Interop.Constants.CreateSuspended | Interop.Constants.StackSizeParamIsAReservation),
                out threadId);

            if (_osHandle.IsInvalid)
            {
                return false;
            }

            // CoreCLR ignores OS errors while setting the priority, so do we
            SetPriorityLive(_priority);

            Interop.mincore.ResumeThread(_osHandle);
            return true;
        }

        /// <summary>
        /// This is an entry point for managed threads created by application
        /// </summary>
        [NativeCallable(CallingConvention = CallingConvention.StdCall)]
        private static uint ThreadEntryPoint(IntPtr parameter)
        {
            StartThread(parameter);
            return 0;
        }

        public ApartmentState GetApartmentState()
        {
            if (this != CurrentThread)
            {
                if (HasStarted())
                    throw new ThreadStateException();
                return _initialAppartmentState;
            }

            switch (GetCurrentApartmentType())
            {
                case ApartmentType.STA:
                    return ApartmentState.STA;
                case ApartmentType.MTA:
                    return ApartmentState.MTA;
                default:
                    return ApartmentState.Unknown;
            }
        }

        public bool TrySetApartmentState(ApartmentState state)
        {
            if (this != CurrentThread)
            {
                using (LockHolder.Hold(_lock))
                {
                    if (HasStarted())
                        throw new ThreadStateException();
                    _initialAppartmentState = state;
                    return true;
                }
            }

            if (state != ApartmentState.Unknown)
            {
                InitializeCom(state);
            }
            else
            {
                UninitializeCom();
            }

            // Clear the cache and check whether new state matches the desired state
            t_apartmentType = ApartmentType.Unknown;
            return state == GetApartmentState();
        }

        private void InitializeComOnNewThread()
        {
            InitializeCom(_initialAppartmentState);
        }

        internal static void InitializeCom(ApartmentState state = ApartmentState.MTA)
        {
            if (t_comInitializedByUs)
                return;

#if ENABLE_WINRT
            int hr = Interop.WinRT.RoInitialize(
                (state == ApartmentState.STA) ? Interop.WinRT.RO_INIT_SINGLETHREADED
                    : Interop.WinRT.RO_INIT_MULTITHREADED);
#else
            int hr = Interop.Ole32.CoInitializeEx(IntPtr.Zero,
                (state == ApartmentState.STA) ? Interop.Ole32.COINIT_APARTMENTTHREADED
                    : Interop.Ole32.COINIT_MULTITHREADED);
#endif
            // RPC_E_CHANGED_MODE indicates this thread has been already initialized with a different
            // concurrency model. We stay away and let whoever else initialized the COM to be in control.
            if (hr == HResults.RPC_E_CHANGED_MODE)
                return;
            if (hr < 0)
                throw new OutOfMemoryException();

            t_comInitializedByUs = true;

            // If the thread has already been CoInitialized to the proper mode, then
            // we don't want to leave an outstanding CoInit so we CoUninit.
            if (hr > 0)
                UninitializeCom();
        }

        private static void UninitializeCom()
        {
            if (!t_comInitializedByUs)
                return;

#if ENABLE_WINRT
            Interop.WinRT.RoUninitialize();
#else
            Interop.Ole32.CoUninitialize();
#endif
            t_comInitializedByUs = false;
        }

        // TODO: https://github.com/dotnet/corefx/issues/20766
        public void DisableComObjectEagerCleanup() { }
        public void Interrupt() { throw new PlatformNotSupportedException(); }

        internal static void UninterruptibleSleep0()
        {
            Interop.mincore.Sleep(0);
        }

        private static void SleepInternal(int millisecondsTimeout)
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

            Interop.APTTYPE aptType;
            Interop.APTTYPEQUALIFIER aptTypeQualifier;
            int result = Interop.Ole32.CoGetApartmentType(out aptType, out aptTypeQualifier);

            ApartmentType type = ApartmentType.Unknown;

            switch (result)
            {
                case HResults.CO_E_NOTINITIALIZED:
                    type = ApartmentType.None;
                    break;

                case HResults.S_OK:
                    switch (aptType)
                    {
                        case Interop.APTTYPE.APTTYPE_STA:
                        case Interop.APTTYPE.APTTYPE_MAINSTA:
                            type = ApartmentType.STA;
                            break;

                        case Interop.APTTYPE.APTTYPE_MTA:
                            type = ApartmentType.MTA;
                            break;

                        case Interop.APTTYPE.APTTYPE_NA:
                            switch (aptTypeQualifier)
                            {
                                case Interop.APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_MTA:
                                case Interop.APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_IMPLICIT_MTA:
                                    type = ApartmentType.MTA;
                                    break;

                                case Interop.APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_STA:
                                case Interop.APTTYPEQUALIFIER.APTTYPEQUALIFIER_NA_ON_MAINSTA:
                                    type = ApartmentType.STA;
                                    break;

                                default:
                                    Debug.Fail("NA apartment without NA qualifier");
                                    break;
                            }
                            break;
                    }
                    break;

                default:
                    Debug.Fail("bad return from CoGetApartmentType");
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

        private static int ComputeCurrentProcessorId() => (int)Interop.mincore.GetCurrentProcessorNumber();
    }
}
