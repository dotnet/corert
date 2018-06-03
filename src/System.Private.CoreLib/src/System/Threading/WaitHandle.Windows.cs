// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;
using System.Runtime;
using Internal.Runtime.Augments;
using Microsoft.Win32.SafeHandles;

namespace System.Threading
{
    public abstract partial class WaitHandle
    {
        internal static unsafe int WaitForSingleObject(IntPtr handle, int millisecondsTimeout, bool interruptible)
        {
            if (interruptible)
            {
                SynchronizationContext context = RuntimeThread.CurrentThread.SynchronizationContext;
                bool useSyncContextWait = (context != null) && context.IsWaitNotificationRequired();

                if (useSyncContextWait)
                {
                    var handles = new IntPtr[1] { handle };
                    return context.Wait(handles, false, millisecondsTimeout);
                }
            }

            return WaitForMultipleObjectsIgnoringSyncContext(&handle, 1, false, millisecondsTimeout, interruptible);
        }

        internal static unsafe int WaitForMultipleObjectsIgnoringSyncContext(IntPtr[] handles, int numHandles, bool waitAll, int millisecondsTimeout)
        {
            fixed (IntPtr* pHandles = handles)
            {
                return WaitForMultipleObjectsIgnoringSyncContext(pHandles, numHandles, waitAll, millisecondsTimeout, true);
            }
        }

        private static unsafe int WaitForMultipleObjectsIgnoringSyncContext(IntPtr* pHandles, int numHandles, bool waitAll, int millisecondsTimeout, bool interruptible)
        {
            Debug.Assert(millisecondsTimeout >= -1);

            //
            // In the CLR, we use CoWaitForMultipleHandles to pump messages while waiting in an STA.  In that case, we cannot use WAIT_ALL.  
            // That's because the wait would only be satisfied if a message arrives while the handles are signalled.
            //
            if (waitAll)
            {
                if (numHandles == 1)
                    waitAll = false;
                else if (RuntimeThread.GetCurrentApartmentType() == RuntimeThread.ApartmentType.STA)
                    throw new NotSupportedException(SR.NotSupported_WaitAllSTAThread);
            }

            RuntimeThread currentThread = RuntimeThread.CurrentThread;
            currentThread.SetWaitSleepJoinState();

            int result;
            if (RuntimeThread.ReentrantWaitsEnabled)
            {
                Debug.Assert(!waitAll);
                result = RuntimeImports.RhCompatibleReentrantWaitAny(false, millisecondsTimeout, numHandles, pHandles);
            }
            else
            {
                result = (int)Interop.mincore.WaitForMultipleObjectsEx((uint)numHandles, (IntPtr)pHandles, waitAll, (uint)millisecondsTimeout, false);
            }

            currentThread.ClearWaitSleepJoinState();

            if (result == WaitHandle.WaitFailed)
            {
                int errorCode = Interop.mincore.GetLastError();
                if (waitAll && errorCode == Interop.Errors.ERROR_INVALID_PARAMETER)
                {
                    // Check for duplicate handles. This is a brute force O(n^2) search, which is intended since the typical
                    // array length is short enough that this would actually be faster than using a hash set. Also, the worst
                    // case is not so bad considering that the array length is limited by
                    // <see cref="WaitHandle.MaxWaitHandles"/>.
                    for (int i = 1; i < numHandles; ++i)
                    {
                        IntPtr handle = pHandles[i];
                        for (int j = 0; j < i; ++j)
                        {
                            if (pHandles[j] == handle)
                            {
                                throw new DuplicateWaitObjectException("waitHandles[" + i + ']');
                            }
                        }
                    }
                }

                ThrowWaitFailedException(errorCode);
            }

            return result;
        }

        private static bool WaitOneCore(IntPtr handle, int millisecondsTimeout, bool interruptible)
        {
            Debug.Assert(millisecondsTimeout >= -1);

            int ret = WaitForSingleObject(handle, millisecondsTimeout, interruptible);

            if (ret == WaitAbandoned)
            {
                ThrowAbandonedMutexException();
            }

            return ret != WaitTimeout;
        }

        /*========================================================================
        ** Waits for signal from all the objects. 
        ** timeout indicates how long to wait before the method returns.
        ** This method will return either when all the object have been pulsed
        ** or timeout milliseonds have elapsed.
        ========================================================================*/
        private static int WaitMultiple(
            RuntimeThread currentThread,
            SafeWaitHandle[] safeWaitHandles,
            int count,
            int millisecondsTimeout,
            bool waitAll)
        {
            Debug.Assert(currentThread == RuntimeThread.CurrentThread);
            Debug.Assert(safeWaitHandles != null);
            Debug.Assert(safeWaitHandles.Length >= count);

            // If we need to call SynchronizationContext.Wait method, always allocate a new IntPtr[]
            SynchronizationContext context = currentThread.SynchronizationContext;
            bool useSyncContextWait = (context != null) && context.IsWaitNotificationRequired();

            IntPtr[] rentedHandles = useSyncContextWait ? null : currentThread.RentWaitedHandleArray(count);
            IntPtr[] handles = rentedHandles ?? new IntPtr[count];
            try
            {
                for (int i = 0; i < count; i++)
                {
                    handles[i] = safeWaitHandles[i].DangerousGetHandle();
                }

                if (useSyncContextWait)
                {
                    return context.Wait(handles, waitAll, millisecondsTimeout);
                }

                return WaitForMultipleObjectsIgnoringSyncContext(handles, count, waitAll, millisecondsTimeout);
            }
            finally
            {
                if (rentedHandles != null)
                {
                    currentThread.ReturnWaitedHandleArray(rentedHandles);
                }
            }
        }

        private static int WaitAnyCore(
            RuntimeThread currentThread,
            SafeWaitHandle[] safeWaitHandles,
            WaitHandle[] waitHandles,
            int numWaitHandles,
            int millisecondsTimeout)
        {
            Debug.Assert(currentThread == RuntimeThread.CurrentThread);
            Debug.Assert(safeWaitHandles != null);
            Debug.Assert(safeWaitHandles.Length >= numWaitHandles);
            Debug.Assert(waitHandles != null);
            Debug.Assert(numWaitHandles > 0);
            Debug.Assert(numWaitHandles <= MaxWaitHandles);
            Debug.Assert(millisecondsTimeout >= -1);

            int ret = WaitMultiple(currentThread, safeWaitHandles, numWaitHandles, millisecondsTimeout, false /* waitany*/ );

            if ((WaitAbandoned <= ret) && (WaitAbandoned + numWaitHandles > ret))
            {
                int mutexIndex = ret - WaitAbandoned;
                if (0 <= mutexIndex && mutexIndex < waitHandles.Length)
                {
                    ThrowAbandonedMutexException(mutexIndex, waitHandles[mutexIndex]);
                }
                else
                {
                    ThrowAbandonedMutexException();
                }
            }

            return ret;
        }

        private static bool WaitAllCore(
            RuntimeThread currentThread,
            SafeWaitHandle[] safeWaitHandles,
            WaitHandle[] waitHandles,
            int millisecondsTimeout)
        {
            Debug.Assert(currentThread == RuntimeThread.CurrentThread);
            Debug.Assert(safeWaitHandles != null);
            Debug.Assert(safeWaitHandles.Length >= waitHandles.Length);
            Debug.Assert(millisecondsTimeout >= -1);

            int ret = WaitMultiple(currentThread, safeWaitHandles, waitHandles.Length, millisecondsTimeout, true /* waitall*/ );

            if ((WaitAbandoned <= ret) && (WaitAbandoned + waitHandles.Length > ret))
            {
                //In the case of WaitAll the OS will only provide the
                //    information that mutex was abandoned.
                //    It won't tell us which one.  So we can't set the Index or provide access to the Mutex
                ThrowAbandonedMutexException();
            }

            return ret != WaitTimeout;
        }

        private static bool SignalAndWaitCore(IntPtr handleToSignal, IntPtr handleToWaitOn, int millisecondsTimeout)
        {
            Debug.Assert(millisecondsTimeout >= -1);

            int ret = (int)Interop.mincore.SignalObjectAndWait(handleToSignal, handleToWaitOn, (uint)millisecondsTimeout, false);

            if (ret == WaitAbandoned)
            {
                ThrowAbandonedMutexException();
            }

            if (ret == WaitFailed)
            {
                ThrowWaitFailedException(Interop.mincore.GetLastError());
            }

            return ret != WaitTimeout;
        }

        private static void ThrowAbandonedMutexException()
        {
            throw new AbandonedMutexException();
        }

        private static void ThrowAbandonedMutexException(int location, WaitHandle handle)
        {
            throw new AbandonedMutexException(location, handle);
        }

        internal static void ThrowSignalOrUnsignalException()
        {
            int errorCode = Interop.mincore.GetLastError();
            switch (errorCode)
            {
                case Interop.Errors.ERROR_INVALID_HANDLE:
                    ThrowInvalidHandleException();
                    break;

                case Interop.Errors.ERROR_TOO_MANY_POSTS:
                    throw new SemaphoreFullException();

                case Interop.Errors.ERROR_NOT_OWNER:
                    throw new ApplicationException(SR.Arg_SynchronizationLockException);

                default:
                    var ex = new Exception();
                    ex.HResult = errorCode;
                    throw ex;
            }
        }

        private static void ThrowWaitFailedException(int errorCode)
        {
            switch (errorCode)
            {
                case Interop.Errors.ERROR_INVALID_HANDLE:
                    ThrowInvalidHandleException();
                    break;

                case Interop.Errors.ERROR_INVALID_PARAMETER:
                    throw new ArgumentException();

                case Interop.Errors.ERROR_ACCESS_DENIED:
                    throw new UnauthorizedAccessException();

                case Interop.Errors.ERROR_NOT_ENOUGH_MEMORY:
                    throw new OutOfMemoryException();

                case Interop.Errors.ERROR_TOO_MANY_POSTS:
                    // Only applicable to <see cref="WaitHandle.SignalAndWait(WaitHandle, WaitHandle)"/>. Note however, that
                    // if the semahpore already has the maximum signal count, the Windows SignalObjectAndWait function does not
                    // return an error, but this code is kept for historical reasons and to convey the intent, since ideally,
                    // that should be an error.
                    throw new InvalidOperationException(SR.Threading_SemaphoreFullException);

                case Interop.Errors.ERROR_NOT_OWNER:
                    // Only applicable to <see cref="WaitHandle.SignalAndWait(WaitHandle, WaitHandle)"/> when signaling a mutex
                    // that is locked by a different thread. Note that if the mutex is already unlocked, the Windows
                    // SignalObjectAndWait function does not return an error.
                    throw new ApplicationException(SR.Arg_SynchronizationLockException);

                case Interop.Errors.ERROR_MUTANT_LIMIT_EXCEEDED:
                    throw new OverflowException(SR.Overflow_MutexReacquireCount);

                default:
                    Exception ex = new Exception();
                    ex.HResult = errorCode;
                    throw ex;
            }
        }

        internal static Exception ExceptionFromCreationError(int errorCode, string path)
        {
            switch (errorCode)
            {
                case Interop.Errors.ERROR_PATH_NOT_FOUND:
                    return new IOException(SR.Format(SR.IO_PathNotFound_Path, path));

                case Interop.Errors.ERROR_ACCESS_DENIED:
                    return new UnauthorizedAccessException(SR.Format(SR.UnauthorizedAccess_IODenied_Path, path));

                case Interop.Errors.ERROR_ALREADY_EXISTS:
                    return new IOException(SR.Format(SR.IO_AlreadyExists_Name, path));

                case Interop.Errors.ERROR_FILENAME_EXCED_RANGE:
                    return new PathTooLongException();

                default:
                    return new IOException(SR.Arg_IOException, errorCode);
            }
        }
    }
}
