// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace System.Threading
{
    public sealed partial class Semaphore
    {
        private const uint AccessRights = (uint)(Interop.Constants.Synchronize | Interop.Constants.SemaphoreModifyState);

        private Semaphore(SafeWaitHandle handle)
        {
            SafeWaitHandle = handle;
        }

        private static void VerifyNameForCreate(string name)
        {
            if (null != name && MAX_PATH < name.Length)
            {
                throw new ArgumentException(SR.Format(SR.Argument_WaitHandleNameTooLong, Interop.Constants.MaxPath), nameof(name));
            }
        }

        private void CreateSemaphoreCore(int initialCount, int maximumCount, string name, out bool createdNew)
        {
            Debug.Assert(initialCount >= 0);
            Debug.Assert(maximumCount >= 1);
            Debug.Assert(initialCount <= maximumCount);
            Debug.Assert(name == null || name.Length <= MAX_PATH);

            SafeWaitHandle myHandle = Interop.mincore.CreateSemaphoreEx(IntPtr.Zero, initialCount, maximumCount, name, 0, AccessRights);

            if (myHandle.IsInvalid)
            {
                int errorCode = Marshal.GetLastWin32Error();
                if (null != name && 0 != name.Length && Interop.Errors.ERROR_INVALID_HANDLE == errorCode)
                    throw new WaitHandleCannotBeOpenedException(SR.Format(SR.Threading_WaitHandleCannotBeOpenedException_InvalidHandle, name));
                throw ExceptionFromCreationError(errorCode, name);
            }
            else if (name != null)
            {
                int errorCode = Marshal.GetLastWin32Error();
                createdNew = errorCode != Interop.Errors.ERROR_ALREADY_EXISTS;
            }
            else
            {
                createdNew = true;
            }

            SafeWaitHandle = myHandle;
        }

        private static OpenExistingResult OpenExistingWorker(string name, out Semaphore result)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (name.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyName, nameof(name));
            }
            if (null != name && MAX_PATH < name.Length)
            {
                throw new ArgumentException(SR.Format(SR.Argument_WaitHandleNameTooLong, MAX_PATH), nameof(name));
            }

            result = null;

            SafeWaitHandle myHandle = Interop.mincore.OpenSemaphore(AccessRights, false, name);

            if (myHandle.IsInvalid)
            {
                int errorCode = Marshal.GetLastWin32Error();

                if (Interop.Errors.ERROR_FILE_NOT_FOUND == errorCode || Interop.Errors.ERROR_INVALID_NAME == errorCode)
                    return OpenExistingResult.NameNotFound;
                if (Interop.Errors.ERROR_PATH_NOT_FOUND == errorCode)
                    return OpenExistingResult.PathNotFound;
                if (null != name && 0 != name.Length && Interop.Errors.ERROR_INVALID_HANDLE == errorCode)
                    return OpenExistingResult.NameInvalid;
                //this is for passed through NativeMethods Errors
                throw ExceptionFromCreationError(errorCode, name);
            }
            result = new Semaphore(myHandle);
            return OpenExistingResult.Success;
        }

        private static int ReleaseCore(IntPtr handle, int releaseCount)
        {
            Debug.Assert(releaseCount > 0);

            //If ReleaseSempahore returns false when the specified value would cause
            //   the semaphore's count to exceed the maximum count set when Semaphore was created
            //Non-Zero return 
            int previousCount;
            if (!Interop.mincore.ReleaseSemaphore(handle, releaseCount, out previousCount))
            {
                ThrowSignalOrUnsignalException();
            }
            return previousCount;
        }
    }
}
