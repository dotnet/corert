// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: Class to represent all synchronization objects in the runtime (that allow multiple wait)
**
**
=============================================================================*/

using System.Threading;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics.Contracts;
using System.IO;

namespace System.Threading
{
    public abstract class WaitHandle : IDisposable
    {
        public const int WaitTimeout = LowLevelThread.WAIT_TIMEOUT;
        protected static readonly IntPtr InvalidHandle = Interop.InvalidHandleValue;

        internal SafeWaitHandle waitHandle;

        internal enum OpenExistingResult
        {
            Success,
            NameNotFound,
            PathNotFound,
            NameInvalid
        }

        protected WaitHandle()
        {
        }

        public SafeWaitHandle SafeWaitHandle
        {
            get
            {
                if (waitHandle == null)
                {
                    waitHandle = new SafeWaitHandle(InvalidHandle, false);
                }
                return waitHandle;
            }

            set
            { waitHandle = value; }
        }

        public virtual bool WaitOne(int millisecondsTimeout)
        {
            if (millisecondsTimeout < -1)
            {
                throw new ArgumentOutOfRangeException("millisecondsTimeout", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }
            Contract.EndContractBlock();
            return WaitOne((long)millisecondsTimeout);
        }

        public virtual bool WaitOne(TimeSpan timeout)
        {
            long tm = (long)timeout.TotalMilliseconds;
            if (-1 > tm || (long)Int32.MaxValue < tm)
            {
                throw new ArgumentOutOfRangeException("timeout", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }
            return WaitOne(tm);
        }

        public virtual bool WaitOne()
        {
            //Infinite Timeout
            return WaitOne(-1);
        }

        private bool WaitOne(long timeout)
        {
            return InternalWaitOne(waitHandle, timeout);
        }

        internal static bool InternalWaitOne(SafeWaitHandle waitableSafeHandle, long millisecondsTimeout)
        {
            if (waitableSafeHandle == null)
            {
                throw new ObjectDisposedException(null, SR.ObjectDisposed_Generic);
            }
            Contract.EndContractBlock();
            int ret = WaitOneNative(waitableSafeHandle, millisecondsTimeout);

#if FEATURE_LEGACYNETCFFAS
            if (AppDomainPauseManager.IsPaused)
                AppDomainPauseManager.ResumeEvent.WaitOneWithoutFAS();
#endif // FEATURE_LEGACYNETCFFAS

            if (ret == LowLevelThread.WAIT_ABANDONED)
            {
                ThrowAbandonedMutexException();
            }
            return (ret != WaitTimeout);
        }

#if FEATURE_LEGACYNETCFFAS
        internal bool WaitOneWithoutFAS()
        {
            // version of waitone without fast application switch (FAS) support
            // This is required to support the Wait which FAS needs (otherwise recursive dependency comes in)
            if (safeWaitHandle == null)
            {
                throw new ObjectDisposedException(null, SR.ObjectDisposed_Generic);
            }
            Contract.EndContractBlock();

            long timeout = -1;
            int ret = WaitOneNative(safeWaitHandle, (uint)timeout, false);
            if (ret == WAIT_ABANDONED)
            {
                ThrowAbandonedMutexException();
            }
            return (ret != WaitTimeout);
        }
#endif // FEATURE_LEGACYNETCFFAS

        internal static int WaitOneNative(SafeWaitHandle waitableSafeHandle, long millisecondsTimeout)
        {
            Contract.Assert(millisecondsTimeout >= -1 && millisecondsTimeout <= int.MaxValue);

            waitableSafeHandle.DangerousAddRef();
            try
            {
                return LowLevelThread.WaitForSingleObject(waitableSafeHandle.DangerousGetHandle(), (int)millisecondsTimeout);
            }
            finally
            {
                waitableSafeHandle.DangerousRelease();
            }
        }


        /*========================================================================
        ** Waits for signal from all the objects. 
        ** timeout indicates how long to wait before the method returns.
        ** This method will return either when all the object have been pulsed
        ** or timeout milliseonds have elapsed.
        ========================================================================*/
        private static int WaitMultiple(WaitHandle[] waitHandles, int millisecondsTimeout, bool WaitAll)
        {
            IntPtr[] handles = new IntPtr[waitHandles.Length];
            for (int i = 0; i < handles.Length; i++)
            {
                waitHandles[i].waitHandle.DangerousAddRef();
                handles[i] = waitHandles[i].waitHandle.DangerousGetHandle();
            }
            try
            {
                return LowLevelThread.WaitForMultipleObjects(handles, WaitAll, millisecondsTimeout);
            }
            finally
            {
                for (int i = 0; i < handles.Length; i++)
                {
                    waitHandles[i].waitHandle.DangerousRelease();
                }
            }
        }

        public static bool WaitAll(WaitHandle[] waitHandles, int millisecondsTimeout)
        {
            if (waitHandles == null)
            {
                throw new ArgumentNullException("waitHandles", SR.ArgumentNull_Waithandles);
            }
            if (waitHandles.Length == 0)
            {
                //
                // Some history: in CLR 1.0 and 1.1, we threw ArgumentException in this case, which was correct.
                // Somehow, in 2.0, this became ArgumentNullException.  This was not fixed until Silverlight 2,
                // which went back to ArgumentException.
                //
                // Now we're in a bit of a bind.  Backward-compatibility requires us to keep throwing ArgumentException
                // in CoreCLR, and ArgumentNullException in the desktop CLR.  This is ugly, but so is breaking
                // user code.
                //
                throw new ArgumentNullException("waitHandles", SR.Argument_EmptyWaithandleArray);
            }
            if (waitHandles.Length > LowLevelThread.MAX_WAITHANDLES)
            {
                throw new NotSupportedException(SR.NotSupported_MaxWaitHandles);
            }
            if (-1 > millisecondsTimeout)
            {
                throw new ArgumentOutOfRangeException("millisecondsTimeout", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }
            Contract.EndContractBlock();
            WaitHandle[] internalWaitHandles = new WaitHandle[waitHandles.Length];
            for (int i = 0; i < waitHandles.Length; i++)
            {
                WaitHandle waitHandle = waitHandles[i];

                if (waitHandle == null)
                    throw new ArgumentNullException("waitHandles[" + i + "]", SR.ArgumentNull_ArrayElement);

                internalWaitHandles[i] = waitHandle;
            }
#if DEBUG
            // make sure we do not use waitHandles any more.
            waitHandles = null;
#endif
            int ret = WaitMultiple(internalWaitHandles, millisecondsTimeout, true /* waitall*/ );

#if FEATURE_LEGACYNETCFFAS
            if (AppDomainPauseManager.IsPaused)
                AppDomainPauseManager.ResumeEvent.WaitOneWithoutFAS();
#endif // FEATURE_LEGACYNETCFFAS

            if ((LowLevelThread.WAIT_ABANDONED <= ret) && (LowLevelThread.WAIT_ABANDONED + internalWaitHandles.Length > ret))
            {
                //In the case of WaitAll the OS will only provide the
                //    information that mutex was abandoned.
                //    It won't tell us which one.  So we can't set the Index or provide access to the Mutex
                ThrowAbandonedMutexException();
            }

            GC.KeepAlive(internalWaitHandles);
            return (ret != WaitTimeout);
        }

        public static bool WaitAll(
                                    WaitHandle[] waitHandles,
                                    TimeSpan timeout)
        {
            long tm = (long)timeout.TotalMilliseconds;
            if (-1 > tm || (long)Int32.MaxValue < tm)
            {
                throw new ArgumentOutOfRangeException("timeout", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }
            return WaitAll(waitHandles, (int)tm);
        }

        public static bool WaitAll(WaitHandle[] waitHandles)
        {
            return WaitAll(waitHandles, Timeout.Infinite);
        }

        /*========================================================================
        ** Waits for notification from any of the objects. 
        ** timeout indicates how long to wait before the method returns.
        ** This method will return either when either one of the object have been 
        ** signalled or timeout milliseonds have elapsed.
        ========================================================================*/

        public static int WaitAny(WaitHandle[] waitHandles, int millisecondsTimeout)
        {
            if (waitHandles == null)
            {
                throw new ArgumentNullException("waitHandles", SR.ArgumentNull_Waithandles);
            }
            if (waitHandles.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyWaithandleArray);
            }
            if (LowLevelThread.MAX_WAITHANDLES < waitHandles.Length)
            {
                throw new NotSupportedException(SR.NotSupported_MaxWaitHandles);
            }
            if (-1 > millisecondsTimeout)
            {
                throw new ArgumentOutOfRangeException("millisecondsTimeout", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }
            Contract.EndContractBlock();
            WaitHandle[] internalWaitHandles = new WaitHandle[waitHandles.Length];
            for (int i = 0; i < waitHandles.Length; i++)
            {
                WaitHandle waitHandle = waitHandles[i];

                if (waitHandle == null)
                    throw new ArgumentNullException("waitHandles[" + i + "]", SR.ArgumentNull_ArrayElement);

                internalWaitHandles[i] = waitHandle;
            }
#if DEBUG
            // make sure we do not use waitHandles any more.
            waitHandles = null;
#endif
            int ret = WaitMultiple(internalWaitHandles, millisecondsTimeout, false /* waitany*/ );

#if FEATURE_LEGACYNETCFFAS
            if (AppDomainPauseManager.IsPaused)
                AppDomainPauseManager.ResumeEvent.WaitOneWithoutFAS();
#endif // FEATURE_LEGACYNETCFFAS

            if ((LowLevelThread.WAIT_ABANDONED <= ret) && (LowLevelThread.WAIT_ABANDONED + internalWaitHandles.Length > ret))
            {
                int mutexIndex = ret - LowLevelThread.WAIT_ABANDONED;
                if (0 <= mutexIndex && mutexIndex < internalWaitHandles.Length)
                {
                    ThrowAbandonedMutexException(mutexIndex, internalWaitHandles[mutexIndex]);
                }
                else
                {
                    ThrowAbandonedMutexException();
                }
            }

            GC.KeepAlive(internalWaitHandles);
            return ret;
        }

        public static int WaitAny(
                                    WaitHandle[] waitHandles,
                                    TimeSpan timeout)
        {
            long tm = (long)timeout.TotalMilliseconds;
            if (-1 > tm || (long)Int32.MaxValue < tm)
            {
                throw new ArgumentOutOfRangeException("timeout", SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }
            return WaitAny(waitHandles, (int)tm);
        }

        public static int WaitAny(WaitHandle[] waitHandles)
        {
            return WaitAny(waitHandles, Timeout.Infinite);
        }

        private static void ThrowAbandonedMutexException()
        {
            throw new AbandonedMutexException();
        }

        private static void ThrowAbandonedMutexException(int location, WaitHandle handle)
        {
            throw new AbandonedMutexException(location, handle);
        }

        protected virtual void Dispose(bool explicitDisposing)
        {
            if (waitHandle != null)
            {
                waitHandle.Close();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
