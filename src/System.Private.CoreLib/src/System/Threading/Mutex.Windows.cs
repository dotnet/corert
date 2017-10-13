// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace System.Threading
{
    public sealed partial class Mutex
    {
        private const uint AccessRights = (uint)(Interop.Constants.MaximumAllowed | Interop.Constants.Synchronize | Interop.Constants.MutexModifyState);

        private static void VerifyNameForCreate(string name)
        {
            if (null != name && ((int)Interop.Constants.MaxPath) < name.Length)
            {
                throw new ArgumentException(SR.Format(SR.Argument_WaitHandleNameTooLong, Interop.Constants.MaxPath), nameof(name));
            }
        }

        private void CreateMutexCore(bool initiallyOwned, string name, out bool createdNew)
        {
            Debug.Assert(name == null || name.Length <= (int)Interop.Constants.MaxPath);

            uint mutexFlags = initiallyOwned ? (uint)Interop.Constants.CreateMutexInitialOwner : 0;

            SafeWaitHandle mutexHandle = Interop.mincore.CreateMutexEx(IntPtr.Zero, name, mutexFlags, AccessRights);
            int errorCode = Marshal.GetLastWin32Error();

            if (mutexHandle.IsInvalid)
            {
                mutexHandle.SetHandleAsInvalid();
                if (null != name && 0 != name.Length && Interop.Errors.ERROR_INVALID_HANDLE == errorCode)
                    throw new WaitHandleCannotBeOpenedException(SR.Format(SR.Threading_WaitHandleCannotBeOpenedException_InvalidHandle, name));
                throw ExceptionFromCreationError(errorCode, name);
            }

            createdNew = errorCode != Interop.Errors.ERROR_ALREADY_EXISTS;
            SafeWaitHandle = mutexHandle;
        }

        private static OpenExistingResult OpenExistingWorker(string name, out Mutex result)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name), SR.ArgumentNull_WithParamName);
            }

            if (name.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyName, nameof(name));
            }
            if (((int)Interop.Constants.MaxPath) < (uint)name.Length)
            {
                throw new ArgumentException(SR.Format(SR.Argument_WaitHandleNameTooLong, Interop.Constants.MaxPath), nameof(name));
            }

            result = null;

            SafeWaitHandle myHandle = Interop.mincore.OpenMutex(AccessRights, false, name);

            int errorCode = 0;
            if (myHandle.IsInvalid)
            {
                errorCode = Marshal.GetLastWin32Error();

                if (Interop.Errors.ERROR_FILE_NOT_FOUND == errorCode || Interop.Errors.ERROR_INVALID_NAME == errorCode)
                    return OpenExistingResult.NameNotFound;
                if (Interop.Errors.ERROR_PATH_NOT_FOUND == errorCode)
                    return OpenExistingResult.PathNotFound;
                if (null != name && 0 != name.Length && Interop.Errors.ERROR_INVALID_HANDLE == errorCode)
                    return OpenExistingResult.NameInvalid;

                throw ExceptionFromCreationError(errorCode, name);
            }

            result = new Mutex(myHandle);
            return OpenExistingResult.Success;
        }

        private static void ReleaseMutexCore(IntPtr handle)
        {
            if (!Interop.mincore.ReleaseMutex(handle))
            {
                ThrowSignalOrUnsignalException();
            }
        }
    }
}
