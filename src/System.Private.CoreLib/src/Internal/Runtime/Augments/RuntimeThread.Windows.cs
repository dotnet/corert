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

            // Throw an ApplicationException for compatibility with CoreCLR
            var ex = new ApplicationException();
            ex.SetErrorCode(Marshal.GetLastWin32Error());
            throw ex;
        }

        private static ThreadPriority MapFromOSPriority(OSThreadPriority priority)
        {
            switch (priority)
            {
                case OSThreadPriority.Idle:
                case OSThreadPriority.Lowest:
                    return ThreadPriority.Lowest;

                case OSThreadPriority.BelowNormal:
                    return ThreadPriority.BelowNormal;

                case OSThreadPriority.Normal:
                    return ThreadPriority.Normal;

                case OSThreadPriority.AboveNormal:
                    return ThreadPriority.AboveNormal;

                case OSThreadPriority.Highest:
                case OSThreadPriority.TimeCritical:
                    return ThreadPriority.Highest;

                case OSThreadPriority.ErrorReturn:
                    Debug.Fail("GetThreadPriority failed");
                    return ThreadPriority.Normal;

                default:
                    return ThreadPriority.Normal;
            }
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
                    Environment.FailFast("Unreached");
                    return OSThreadPriority.Normal;
            }
        }

        private bool SetPriority(ThreadPriority priority)
        {
            if (_osHandle.IsInvalid)
            {
                return true;
            }
            return Interop.mincore.SetThreadPriority(_osHandle, (int)MapToOSPriority(priority));
        }

        /// <summary>
        /// Checks if the underlying OS thread has finished execution.
        /// </summary>
        private bool HasFinishedExecution()
        {
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

                // TODO: OOM hardening, _maxStackSize
                GCHandle threadHandle = GCHandle.Alloc(this);
                _threadStartArg = parameter;

                try
                {
                    uint threadId;

                    _osHandle = new SafeWaitHandle(Interop.mincore.CreateThread(IntPtr.Zero, IntPtr.Zero,
                        AddrofIntrinsics.AddrOf<Interop.mincore.ThreadProc>(StartThread), (IntPtr)threadHandle, 0, out threadId),
                        ownsHandle: true);

                    // Ignore errors (as in CoreCLR)
                    SetPriority(_priority);

                    // Wait until the new thread is started (as in CoreCLR)
                    while (GetThreadStateBit(ThreadState.Unstarted))
                    {
                        Yield();
                    }
                }
                finally
                {
                    if (_osHandle == null)
                    {
                        threadHandle.Free();
                        _threadStartArg = null;
                    }
                }
            }
        }

        [NativeCallable(CallingConvention = CallingConvention.StdCall)]
        private static uint StartThread(IntPtr parameter)
        {
            GCHandle threadHandle = (GCHandle)parameter;
            RuntimeThread thread = (RuntimeThread)threadHandle.Target;
            t_currentThread = thread;
            System.Threading.ManagedThreadId.SetForCurrentThread(thread._managedThreadId);
            threadHandle.Free();

            thread.ClearThreadStateBit(ThreadState.Unstarted);

            try
            {
                Delegate threadStart = thread._threadStart;
                object threadStartArg = thread._threadStartArg;
                thread._threadStart = null;
                thread._threadStartArg = null;

#if ENABLE_WINRT
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
