// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Runtime.Versioning;

namespace System.Threading
{
    [ComVisibleAttribute(false)]
    public sealed class Semaphore : WaitHandle
    {
        private const int MAX_PATH = (int)Interop.Constants.MaxPath;

        // creates a nameless semaphore object
        // Win32 only takes maximum count of Int32.MaxValue
        public Semaphore(int initialCount, int maximumCount) : this(initialCount, maximumCount, null) { }

        public Semaphore(int initialCount, int maximumCount, string name)
        {
            if (initialCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCount), SR.ArgumentOutOfRange_NeedNonNegNumRequired);
            }

            if (maximumCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCount), SR.ArgumentOutOfRange_NeedPosNum);
            }

            if (initialCount > maximumCount)
            {
                throw new ArgumentException(SR.Argument_SemaphoreInitialMaximum);
            }

            if (null != name && MAX_PATH < name.Length)
            {
                throw new ArgumentException(SR.Argument_WaitHandleNameTooLong);
            }
            SafeWaitHandle myHandle = new SafeWaitHandle(Interop.mincore.CreateSemaphoreEx(IntPtr.Zero, initialCount, maximumCount, name, 0, (uint)(Interop.Constants.SemaphoreModifyState | Interop.Constants.Synchronize)), true);

            if (myHandle.IsInvalid)
            {
                int errorCode = (int)Interop.mincore.GetLastError();

                if (null != name && 0 != name.Length && Interop.mincore.Errors.ERROR_INVALID_HANDLE == errorCode)
                    throw new WaitHandleCannotBeOpenedException(SR.Format(SR.Threading_WaitHandleCannotBeOpenedException_InvalidHandle, name));

                throw ExceptionFromCreationError(errorCode, name);
            }

            SafeWaitHandle = myHandle;
        }

        public unsafe Semaphore(int initialCount, int maximumCount, string name, out bool createdNew)
        {
            if (initialCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCount), SR.ArgumentOutOfRange_NeedNonNegNumRequired);
            }

            if (maximumCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCount), SR.ArgumentOutOfRange_NeedNonNegNumRequired);
            }

            if (initialCount > maximumCount)
            {
                throw new ArgumentException(SR.Argument_SemaphoreInitialMaximum);
            }

            if (null != name && MAX_PATH < name.Length)
            {
                throw new ArgumentException(SR.Argument_WaitHandleNameTooLong);
            }

            SafeWaitHandle myHandle;
            myHandle = new SafeWaitHandle(Interop.mincore.CreateSemaphoreEx(IntPtr.Zero, initialCount, maximumCount, name, 0, (uint)(Interop.Constants.SemaphoreModifyState | Interop.Constants.Synchronize)), true);

            int errorCode = (int)Interop.mincore.GetLastError();
            if (myHandle.IsInvalid)
            {
                if (null != name && 0 != name.Length && Interop.mincore.Errors.ERROR_INVALID_HANDLE == errorCode)
                    throw new WaitHandleCannotBeOpenedException(SR.Format(SR.Threading_WaitHandleCannotBeOpenedException_InvalidHandle, name));
                throw ExceptionFromCreationError(errorCode, name);
            }
            createdNew = errorCode != Interop.mincore.Errors.ERROR_ALREADY_EXISTS;

            SafeWaitHandle = myHandle;
        }

        private Semaphore(SafeWaitHandle handle)
        {
            SafeWaitHandle = handle;
        }

        public static Semaphore OpenExisting(string name)
        {
            Semaphore result;
            switch (OpenExistingWorker(name, out result))
            {
                case OpenExistingResult.NameNotFound:
                    throw new WaitHandleCannotBeOpenedException();
                case OpenExistingResult.NameInvalid:
                    throw new WaitHandleCannotBeOpenedException(SR.Format(SR.Threading_WaitHandleCannotBeOpenedException_InvalidHandle, name));
                case OpenExistingResult.PathNotFound:
                    throw new IOException(SR.Format(SR.IO_PathNotFound_Path, name));
                default:
                    return result;
            }
        }

        public static bool TryOpenExisting(string name, out Semaphore result)
        {
            return OpenExistingWorker(name, out result) == OpenExistingResult.Success;
        }

        private static OpenExistingResult OpenExistingWorker(
            string name,
            out Semaphore result)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (name.Length == 0)
            {
                throw new ArgumentException(SR.Format(SR.InvalidNullEmptyArgument, nameof(name)), nameof(name));
            }
            if (null != name && MAX_PATH < name.Length)
            {
                throw new ArgumentException(SR.Argument_WaitHandleNameTooLong);
            }

            result = null;

            //Pass false to OpenSemaphore to prevent inheritedHandles
            SafeWaitHandle myHandle = new SafeWaitHandle(Interop.mincore.OpenSemaphore((uint)(Interop.Constants.SemaphoreModifyState | Interop.Constants.Synchronize), false, name), true);

            if (myHandle.IsInvalid)
            {
                int errorCode = (int)Interop.mincore.GetLastError();

                if (Interop.mincore.Errors.ERROR_FILE_NOT_FOUND == errorCode || Interop.mincore.Errors.ERROR_INVALID_NAME == errorCode)
                    return OpenExistingResult.NameNotFound;
                if (Interop.mincore.Errors.ERROR_PATH_NOT_FOUND == errorCode)
                    return OpenExistingResult.PathNotFound;
                if (null != name && 0 != name.Length && Interop.mincore.Errors.ERROR_INVALID_HANDLE == errorCode)
                    return OpenExistingResult.NameInvalid;
                //this is for passed through NativeMethods Errors
                throw ExceptionFromCreationError(errorCode, name);
            }
            result = new Semaphore(myHandle);
            return OpenExistingResult.Success;
        }


        // increase the count on a semaphore, returns previous count
        public int Release()
        {
            return Release(1);
        }

        // increase the count on a semaphore, returns previous count
        public int Release(int releaseCount)
        {
            if (releaseCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(releaseCount), SR.ArgumentOutOfRange_NeedNonNegNumRequired);
            }
            int previousCount;

            //If ReleaseSempahore returns false when the specified value would cause
            //   the semaphore's count to exceed the maximum count set when Semaphore was created
            //Non-Zero return 

            waitHandle.DangerousAddRef();
            try
            {
                if (!Interop.mincore.ReleaseSemaphore(waitHandle.DangerousGetHandle(), releaseCount, out previousCount))
                {
                    throw new SemaphoreFullException();
                }
            }
            finally
            {
                waitHandle.DangerousRelease();
            }

            return previousCount;
        }
    }
}

