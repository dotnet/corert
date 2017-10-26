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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Internal.Runtime.Augments;
using Microsoft.Win32.SafeHandles;

namespace System.Threading
{
    public abstract partial class WaitHandle : MarshalByRefObject, IDisposable
    {
        internal const int WaitObject0 = (int)Interop.Constants.WaitObject0;
        public const int WaitTimeout = (int)Interop.Constants.WaitTimeout;
        internal const int WaitAbandoned = (int)Interop.Constants.WaitAbandoned0;
        internal const int WaitFailed = unchecked((int)Interop.Constants.WaitFailed);
        internal const int MaxWaitHandles = 64;
        protected static readonly IntPtr InvalidHandle = Interop.InvalidHandleValue;

        internal SafeWaitHandle _waitHandle;

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

        [Obsolete("Use the SafeWaitHandle property instead.")]
        public virtual IntPtr Handle
        {
            get
            {
                return _waitHandle == null ? InvalidHandle : _waitHandle.DangerousGetHandle();
            }
            set
            {
                if (value == InvalidHandle)
                {
                    // This line leaks a handle.  However, it's currently
                    // not perfectly clear what the right behavior is here 
                    // anyways.  This preserves Everett behavior.  We should 
                    // ideally do these things:
                    // *) Expose a settable SafeHandle property on WaitHandle.
                    // *) Expose a settable OwnsHandle property on SafeHandle.
                    if (_waitHandle != null)
                    {
                        _waitHandle.SetHandleAsInvalid();
                        _waitHandle = null;
                    }
                }
                else
                {
                    _waitHandle = new SafeWaitHandle(value, true);
                }
            }
        }

        public SafeWaitHandle SafeWaitHandle
        {
            get
            {
                if (_waitHandle == null)
                {
                    _waitHandle = new SafeWaitHandle(InvalidHandle, false);
                }
                return _waitHandle;
            }

            set
            { _waitHandle = value; }
        }

        internal static int ToTimeoutMilliseconds(TimeSpan timeout)
        {
            var timeoutMilliseconds = (long)timeout.TotalMilliseconds;
            if (timeoutMilliseconds < -1 || timeoutMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }
            return (int)timeoutMilliseconds;
        }

        public virtual bool WaitOne(int millisecondsTimeout)
        {
            if (millisecondsTimeout < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }

            return WaitOneCore(millisecondsTimeout);
        }

        private bool WaitOneCore(int millisecondsTimeout, bool interruptible = true)
        {
            Debug.Assert(millisecondsTimeout >= -1);

            // The field value is modifiable via the public <see cref="WaitHandle.SafeWaitHandle"/> property, save it locally
            // to ensure that one instance is used in all places in this method
            SafeWaitHandle waitHandle = _waitHandle;
            if (waitHandle == null)
            {
                // Throw ObjectDisposedException for backward compatibility even though it is not be representative of the issue
                throw new ObjectDisposedException(null, SR.ObjectDisposed_Generic);
            }

            waitHandle.DangerousAddRef();
            try
            {
                return WaitOneCore(waitHandle.DangerousGetHandle(), millisecondsTimeout, interruptible);
            }
            finally
            {
                waitHandle.DangerousRelease();
            }
        }

        public virtual bool WaitOne(TimeSpan timeout) => WaitOneCore(ToTimeoutMilliseconds(timeout));
        public virtual bool WaitOne() => WaitOneCore(Timeout.Infinite);

        public virtual bool WaitOne(int millisecondsTimeout, bool exitContext) => WaitOne(millisecondsTimeout);
        public virtual bool WaitOne(TimeSpan timeout, bool exitContext) => WaitOne(timeout);

        internal bool WaitOne(bool interruptible) => WaitOneCore(Timeout.Infinite, interruptible);

        /// <summary>
        /// Obtains all of the corresponding safe wait handles and adds a ref to each. Since the <see cref="SafeWaitHandle"/>
        /// property is publically modifiable, this makes sure that we add and release refs one the same set of safe wait
        /// handles to keep them alive during a multi-wait operation.
        /// </summary>
        private static SafeWaitHandle[] ObtainSafeWaitHandles(
            RuntimeThread currentThread,
            WaitHandle[] waitHandles,
            int numWaitHandles,
            out SafeWaitHandle[] rentedSafeWaitHandles)
        {
            Debug.Assert(currentThread == RuntimeThread.CurrentThread);
            Debug.Assert(waitHandles != null);
            Debug.Assert(numWaitHandles > 0);
            Debug.Assert(numWaitHandles <= MaxWaitHandles);
            Debug.Assert(numWaitHandles <= waitHandles.Length);

            rentedSafeWaitHandles = currentThread.RentWaitedSafeWaitHandleArray(numWaitHandles);
            SafeWaitHandle[] safeWaitHandles = rentedSafeWaitHandles ?? new SafeWaitHandle[numWaitHandles];
            bool success = false;
            try
            {
                for (int i = 0; i < numWaitHandles; ++i)
                {
                    WaitHandle waitHandle = waitHandles[i];
                    if (waitHandle == null)
                    {
                        throw new ArgumentNullException("waitHandles[" + i + ']', SR.ArgumentNull_ArrayElement);
                    }

                    SafeWaitHandle safeWaitHandle = waitHandle._waitHandle;
                    if (safeWaitHandle == null)
                    {
                        // Throw ObjectDisposedException for backward compatibility even though it is not be representative of the issue
                        throw new ObjectDisposedException(null, SR.ObjectDisposed_Generic);
                    }

                    safeWaitHandle.DangerousAddRef();
                    safeWaitHandles[i] = safeWaitHandle;
                }
                success = true;
            }
            finally
            {
                if (!success)
                {
                    for (int i = 0; i < numWaitHandles; ++i)
                    {
                        SafeWaitHandle safeWaitHandle = safeWaitHandles[i];
                        if (safeWaitHandle == null)
                        {
                            break;
                        }
                        safeWaitHandle.DangerousRelease();
                        safeWaitHandles[i] = null;
                    }

                    if (rentedSafeWaitHandles != null)
                    {
                        currentThread.ReturnWaitedSafeWaitHandleArray(rentedSafeWaitHandles);
                    }
                }
            }

            return safeWaitHandles;
        }

        public static bool WaitAll(WaitHandle[] waitHandles, int millisecondsTimeout)
        {
            if (waitHandles == null)
            {
                throw new ArgumentNullException(nameof(waitHandles), SR.ArgumentNull_Waithandles);
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
                throw new ArgumentNullException(nameof(waitHandles), SR.Argument_EmptyWaithandleArray);
            }
            if (waitHandles.Length > MaxWaitHandles)
            {
                throw new NotSupportedException(SR.NotSupported_MaxWaitHandles);
            }
            if (-1 > millisecondsTimeout)
            {
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }

            RuntimeThread currentThread = RuntimeThread.CurrentThread;
            SafeWaitHandle[] rentedSafeWaitHandles;
            SafeWaitHandle[] safeWaitHandles = ObtainSafeWaitHandles(currentThread, waitHandles, waitHandles.Length, out rentedSafeWaitHandles);
            try
            {
                return WaitAllCore(currentThread, safeWaitHandles, waitHandles, millisecondsTimeout);
            }
            finally
            {
                for (int i = 0; i < waitHandles.Length; ++i)
                {
                    safeWaitHandles[i].DangerousRelease();
                    safeWaitHandles[i] = null;
                }

                if (rentedSafeWaitHandles != null)
                {
                    currentThread.ReturnWaitedSafeWaitHandleArray(rentedSafeWaitHandles);
                }
            }
        }

        public static bool WaitAll(WaitHandle[] waitHandles, TimeSpan timeout) =>
            WaitAll(waitHandles, ToTimeoutMilliseconds(timeout));
        public static bool WaitAll(WaitHandle[] waitHandles) => WaitAll(waitHandles, Timeout.Infinite);

        public static bool WaitAll(WaitHandle[] waitHandles, int millisecondsTimeout, bool exitContext) =>
            WaitAll(waitHandles, millisecondsTimeout);
        public static bool WaitAll(WaitHandle[] waitHandles, TimeSpan timeout, bool exitContext) =>
            WaitAll(waitHandles, timeout);

        /*========================================================================
        ** Waits for notification from any of the objects. 
        ** timeout indicates how long to wait before the method returns.
        ** This method will return either when either one of the object have been 
        ** signalled or timeout milliseonds have elapsed.
        ========================================================================*/

        public static int WaitAny(WaitHandle[] waitHandles, int millisecondsTimeout) => WaitAny(waitHandles, waitHandles?.Length ?? 0, millisecondsTimeout);

        internal static int WaitAny(WaitHandle[] waitHandles, int numWaitHandles, int millisecondsTimeout)
        {
            if (waitHandles == null)
            {
                throw new ArgumentNullException(nameof(waitHandles), SR.ArgumentNull_Waithandles);
            }
            if (waitHandles.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyWaithandleArray);
            }
            if (MaxWaitHandles < waitHandles.Length)
            {
                throw new NotSupportedException(SR.NotSupported_MaxWaitHandles);
            }
            if (-1 > millisecondsTimeout)
            {
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }

            RuntimeThread currentThread = RuntimeThread.CurrentThread;
            SafeWaitHandle[] rentedSafeWaitHandles;
            SafeWaitHandle[] safeWaitHandles = ObtainSafeWaitHandles(currentThread, waitHandles, numWaitHandles, out rentedSafeWaitHandles);
            try
            {
                return WaitAnyCore(currentThread, safeWaitHandles, waitHandles, numWaitHandles, millisecondsTimeout);
            }
            finally
            {
                for (int i = 0; i < numWaitHandles; ++i)
                {
                    safeWaitHandles[i].DangerousRelease();
                    safeWaitHandles[i] = null;
                }

                if (rentedSafeWaitHandles != null)
                {
                    currentThread.ReturnWaitedSafeWaitHandleArray(rentedSafeWaitHandles);
                }
            }
        }

        public static int WaitAny(WaitHandle[] waitHandles, TimeSpan timeout) =>
            WaitAny(waitHandles, ToTimeoutMilliseconds(timeout));
        public static int WaitAny(WaitHandle[] waitHandles) => WaitAny(waitHandles, Timeout.Infinite);

        public static int WaitAny(WaitHandle[] waitHandles, int millisecondsTimeout, bool exitContext) =>
            WaitAny(waitHandles, millisecondsTimeout);
        public static int WaitAny(WaitHandle[] waitHandles, TimeSpan timeout, bool exitContext) =>
            WaitAny(waitHandles, timeout);

        /*=================================================
        ==
        ==  SignalAndWait
        ==
        ==================================================*/

        private static bool SignalAndWait(WaitHandle toSignal, WaitHandle toWaitOn, int millisecondsTimeout)
        {
            if (null == toSignal)
            {
                throw new ArgumentNullException(nameof(toSignal));
            }
            if (null == toWaitOn)
            {
                throw new ArgumentNullException(nameof(toWaitOn));
            }
            if (-1 > millisecondsTimeout)
            {
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }

            // The field value is modifiable via the public <see cref="WaitHandle.SafeWaitHandle"/> property, save it locally
            // to ensure that one instance is used in all places in this method
            SafeWaitHandle safeWaitHandleToSignal = toSignal._waitHandle;
            SafeWaitHandle safeWaitHandleToWaitOn = toWaitOn._waitHandle;
            if (safeWaitHandleToSignal == null || safeWaitHandleToWaitOn == null)
            {
                // Throw ObjectDisposedException for backward compatibility even though it is not be representative of the issue
                throw new ObjectDisposedException(null, SR.ObjectDisposed_Generic);
            }


            safeWaitHandleToSignal.DangerousAddRef();
            try
            {
                safeWaitHandleToWaitOn.DangerousAddRef();
                try
                {
                    return
                        SignalAndWaitCore(
                            safeWaitHandleToSignal.DangerousGetHandle(),
                            safeWaitHandleToWaitOn.DangerousGetHandle(),
                            millisecondsTimeout);
                }
                finally
                {
                    safeWaitHandleToWaitOn.DangerousRelease();
                }
            }
            finally
            {
                safeWaitHandleToSignal.DangerousRelease();
            }
        }

        public static bool SignalAndWait(WaitHandle toSignal, WaitHandle toWaitOn) =>
            SignalAndWait(toSignal, toWaitOn, Timeout.Infinite);
        public static bool SignalAndWait(WaitHandle toSignal, WaitHandle toWaitOn, TimeSpan timeout, bool exitContext) =>
            SignalAndWait(toSignal, toWaitOn, ToTimeoutMilliseconds(timeout));
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread-safety.")]
        public static bool SignalAndWait(WaitHandle toSignal, WaitHandle toWaitOn, int millisecondsTimeout, bool exitContext) =>
            SignalAndWait(toSignal, toWaitOn, millisecondsTimeout);

        public virtual void Close() => Dispose();

        protected virtual void Dispose(bool explicitDisposing)
        {
            if (_waitHandle != null)
            {
                _waitHandle.Close();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal static void ThrowInvalidHandleException()
        {
            var ex = new InvalidOperationException(SR.InvalidOperation_InvalidHandle);
            ex.SetErrorCode(HResults.E_HANDLE);
            throw ex;
        }
    }
}
