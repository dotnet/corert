// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: synchronization primitive that can also be used for interprocess synchronization
**
**
=============================================================================*/

using System;
using System.Threading;
using System.Runtime.CompilerServices;
using System.IO;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Diagnostics.Contracts;

namespace System.Threading
{
    [ComVisible(true)]
    public sealed class Mutex : WaitHandle
    {
        private static bool s_dummyBool;

        public Mutex(bool initiallyOwned, String name, out bool createdNew)
        {
            if (null != name && ((int)Interop.Constants.MaxPath) < (uint)name.Length)
            {
                throw new ArgumentException(SR.Format(SR.Argument_WaitHandleNameTooLong, name));
            }
            Contract.EndContractBlock();

            SafeWaitHandle mutexHandle;
            int errorCode = CreateMutexHandle(initiallyOwned, name, out mutexHandle);
            if (mutexHandle.IsInvalid)
            {
                mutexHandle.SetHandleAsInvalid();
                if (null != name && 0 != name.Length && Interop.mincore.Errors.ERROR_INVALID_HANDLE == errorCode)
                    throw new WaitHandleCannotBeOpenedException(SR.Format(SR.Threading_WaitHandleCannotBeOpenedException_InvalidHandle, name));
                throw ExceptionFromCreationError(errorCode, name);
            }

            createdNew = errorCode != Interop.mincore.Errors.ERROR_ALREADY_EXISTS;

            SafeWaitHandle = mutexHandle;
        }


        public Mutex(bool initiallyOwned, String name)
            : this(initiallyOwned, name, out s_dummyBool)
        {
        }

        public Mutex(bool initiallyOwned)
            : this(initiallyOwned, null, out s_dummyBool)
        {
        }

        public Mutex()
            : this(false, null, out s_dummyBool)
        {
        }

        private Mutex(SafeWaitHandle handle)
        {
            SafeWaitHandle = handle;
        }

        public static Mutex OpenExisting(string name)
        {
            Mutex result;
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

        public static bool TryOpenExisting(string name, out Mutex result)
        {
            return OpenExistingWorker(name, out result) == OpenExistingResult.Success;
        }

        private static OpenExistingResult OpenExistingWorker(string name, out Mutex result)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name", SR.ArgumentNull_WithParamName);
            }

            if (name.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyName, "name");
            }
            if (((int)Interop.Constants.MaxPath) < (uint)name.Length)
            {
                throw new ArgumentException(SR.Format(SR.Argument_WaitHandleNameTooLong, name));
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

                if (Interop.mincore.Errors.ERROR_FILE_NOT_FOUND == errorCode || Interop.mincore.Errors.ERROR_INVALID_NAME == errorCode)
                    return OpenExistingResult.NameNotFound;
                if (Interop.mincore.Errors.ERROR_PATH_NOT_FOUND == errorCode)
                    return OpenExistingResult.PathNotFound;
                if (null != name && 0 != name.Length && Interop.mincore.Errors.ERROR_INVALID_HANDLE == errorCode)
                    return OpenExistingResult.NameInvalid;

                // this is for passed through Win32Native Errors
                throw ExceptionFromCreationError(errorCode, name);
            }

            result = new Mutex(myHandle);
            return OpenExistingResult.Success;
        }

        // Note: To call ReleaseMutex, you must have an ACL granting you
        // MUTEX_MODIFY_STATE rights (0x0001).  The other interesting value
        // in a Mutex's ACL is MUTEX_ALL_ACCESS (0x1F0001).
        public void ReleaseMutex()
        {
            waitHandle.DangerousAddRef();
            try
            {
                if (!Interop.mincore.ReleaseMutex(waitHandle.DangerousGetHandle()))
                    throw new InvalidOperationException(SR.Arg_SynchronizationLockException);
            }
            finally
            {
                waitHandle.DangerousRelease();
            }
        }

        private static int CreateMutexHandle(bool initiallyOwned, String name, out SafeWaitHandle mutexHandle)
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

                if (errorCode == Interop.mincore.Errors.ERROR_ACCESS_DENIED)
                {
                    // If a mutex with the name already exists, OS will try to open it with FullAccess.
                    // It might fail if we don't have enough access. In that case, we try to open the mutex will modify and synchronize access.
                    //
                    mutexHandle = new SafeWaitHandle(Interop.mincore.OpenMutex((uint)(Interop.Constants.MutexModifyState | Interop.Constants.Synchronize), false, name), true);
                    if (!mutexHandle.IsInvalid)
                    {
                        errorCode = Interop.mincore.Errors.ERROR_ALREADY_EXISTS;
                    }
                    else
                    {
                        errorCode = (int)Interop.mincore.GetLastError();
                    }

                    // There could be a race here, the other owner of the mutex can free the mutex,
                    // We need to retry creation in that case.
                    if (errorCode != Interop.mincore.Errors.ERROR_FILE_NOT_FOUND)
                    {
                        if (errorCode == Interop.mincore.Errors.ERROR_SUCCESS)
                        {
                            errorCode = Interop.mincore.Errors.ERROR_ALREADY_EXISTS;
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
    }
}
