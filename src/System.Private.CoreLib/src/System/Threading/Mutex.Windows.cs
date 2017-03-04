// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace System.Threading
{
    public sealed partial class Mutex
    {
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

            SafeWaitHandle mutexHandle;
            int errorCode = CreateMutexHandle(initiallyOwned, name, out mutexHandle);
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

        private static int CreateMutexHandle(bool initiallyOwned, string name, out SafeWaitHandle mutexHandle)
        {
            int errorCode;

            while (true)
            {
                mutexHandle = new SafeWaitHandle(Interop.mincore.CreateMutexEx(IntPtr.Zero, name, initiallyOwned ? (uint)Interop.Constants.CreateMutexInitialOwner : 0, (uint)Interop.Constants.MutexAllAccess), true);
                errorCode = (int)Interop.mincore.GetLastError();
                if (!mutexHandle.IsInvalid)
                {
                    break;
                }

                if (errorCode == Interop.Errors.ERROR_ACCESS_DENIED)
                {
                    // If a mutex with the name already exists, OS will try to open it with FullAccess.
                    // It might fail if we don't have enough access. In that case, we try to open the mutex will modify and synchronize access.
                    //
                    mutexHandle = new SafeWaitHandle(Interop.mincore.OpenMutex((uint)(Interop.Constants.MutexModifyState | Interop.Constants.Synchronize), false, name), true);
                    if (!mutexHandle.IsInvalid)
                    {
                        errorCode = Interop.Errors.ERROR_ALREADY_EXISTS;
                    }
                    else
                    {
                        errorCode = (int)Interop.mincore.GetLastError();
                    }

                    // There could be a race here, the other owner of the mutex can free the mutex,
                    // We need to retry creation in that case.
                    if (errorCode != Interop.Errors.ERROR_FILE_NOT_FOUND)
                    {
                        if (errorCode == Interop.Errors.ERROR_SUCCESS)
                        {
                            errorCode = Interop.Errors.ERROR_ALREADY_EXISTS;
                        }
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
            return errorCode;
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
            Contract.EndContractBlock();

            result = null;

            // To allow users to view & edit the ACL's, call OpenMutex
            // with parameters to allow us to view & edit the ACL.  This will
            // fail if we don't have permission to view or edit the ACL's.  
            // If that happens, ask for less permissions.
            SafeWaitHandle myHandle = new SafeWaitHandle(Interop.mincore.OpenMutex((uint)(Interop.Constants.MutexModifyState | Interop.Constants.Synchronize), false, name), true);

            int errorCode = 0;
            if (myHandle.IsInvalid)
            {
                errorCode = (int)Interop.mincore.GetLastError();

                if (Interop.Errors.ERROR_FILE_NOT_FOUND == errorCode || Interop.Errors.ERROR_INVALID_NAME == errorCode)
                    return OpenExistingResult.NameNotFound;
                if (Interop.Errors.ERROR_PATH_NOT_FOUND == errorCode)
                    return OpenExistingResult.PathNotFound;
                if (null != name && 0 != name.Length && Interop.Errors.ERROR_INVALID_HANDLE == errorCode)
                    return OpenExistingResult.NameInvalid;

                // this is for passed through Win32Native Errors
                throw ExceptionFromCreationError(errorCode, name);
            }

            result = new Mutex(myHandle);
            return OpenExistingResult.Success;
        }

        // Note: To call ReleaseMutexCore, you must have an ACL granting you
        // MUTEX_MODIFY_STATE rights (0x0001).  The other interesting value
        // in a Mutex's ACL is MUTEX_ALL_ACCESS (0x1F0001).
        private static void ReleaseMutexCore(IntPtr handle)
        {
            if (!Interop.mincore.ReleaseMutex(handle))
            {
                ThrowSignalOrUnsignalException();
            }
        }
    }
}
