// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;
using Internal.Runtime.Augments;
using Microsoft.Win32.SafeHandles;

namespace System.Threading
{
    public abstract partial class WaitHandle
    {
        private static bool WaitOneCore(IntPtr handle, int millisecondsTimeout)
        {
            Debug.Assert(millisecondsTimeout >= -1);

            int ret = LowLevelThread.WaitForSingleObject(handle, millisecondsTimeout);

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

            IntPtr[] handles = currentThread.GetWaitedHandleArray(count);
            for (int i = 0; i < count; i++)
            {
                handles[i] = safeWaitHandles[i].DangerousGetHandle();
            }

            return LowLevelThread.WaitForMultipleObjects(handles, count, waitAll, millisecondsTimeout);
        }

        private static int WaitAnyCore(
            RuntimeThread currentThread,
            SafeWaitHandle[] safeWaitHandles,
            WaitHandle[] waitHandles,
            int millisecondsTimeout)
        {
            Debug.Assert(currentThread == RuntimeThread.CurrentThread);
            Debug.Assert(safeWaitHandles != null);
            Debug.Assert(safeWaitHandles.Length >= waitHandles.Length);
            Debug.Assert(waitHandles != null);
            Debug.Assert(waitHandles.Length > 0);
            Debug.Assert(waitHandles.Length <= MaxWaitHandles);
            Debug.Assert(millisecondsTimeout >= -1);

            int ret = WaitMultiple(currentThread, safeWaitHandles, waitHandles.Length, millisecondsTimeout, false /* waitany*/ );

            if ((WaitAbandoned <= ret) && (WaitAbandoned + waitHandles.Length > ret))
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
                LowLevelThread.ThrowWaitFailedException(Interop.mincore.GetLastError());
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
                case Interop.mincore.Errors.ERROR_INVALID_HANDLE:
                    throw InvalidOperationException.NewInvalidHandle();

                case Interop.mincore.Errors.ERROR_TOO_MANY_POSTS:
                    throw new SemaphoreFullException();

                case Interop.mincore.Errors.ERROR_NOT_OWNER:
                    throw new SynchronizationLockException();
                    // TODO: netstandard2.0 - After switching to ns2.0 contracts, use the below instead for compatibility
                    //throw new ApplicationException(SR.Arg_SynchronizationLockException);

                default:
                    var ex = new Exception();
                    ex.SetErrorCode(errorCode);
                    throw ex;
            }
        }

        internal static Exception ExceptionFromCreationError(int errorCode, string path)
        {
            switch (errorCode)
            {
                case Interop.mincore.Errors.ERROR_PATH_NOT_FOUND:
                    return new IOException(SR.Format(SR.IO_PathNotFound_Path, path));

                case Interop.mincore.Errors.ERROR_ACCESS_DENIED:
                    return new UnauthorizedAccessException(SR.Format(SR.UnauthorizedAccess_IODenied_Path, path));

                case Interop.mincore.Errors.ERROR_ALREADY_EXISTS:
                    return new IOException(SR.Format(SR.IO_AlreadyExists_Name, path));

                case Interop.mincore.Errors.ERROR_FILENAME_EXCED_RANGE:
                    return new PathTooLongException();

                default:
                    return new IOException(SR.Arg_IOException, errorCode);
            }
        }
    }
}
