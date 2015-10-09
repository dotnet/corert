// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//

/*=============================================================================
**
**
**
** Purpose: Base class for representing Events
**
**
=============================================================================*/

using System;
using System.Runtime.InteropServices;
using System.Diagnostics.Contracts;
using Microsoft.Win32.SafeHandles;
using System.IO;

namespace System.Threading
{
    [ComVisibleAttribute(true)]
    public class EventWaitHandle : WaitHandle
    {
        [System.Security.SecuritySafeCritical]  // auto-generated
        public EventWaitHandle(bool initialState, EventResetMode mode) : this(initialState, mode, null) { }

        [System.Security.SecurityCritical]  // auto-generated_required
        public EventWaitHandle(bool initialState, EventResetMode mode, string name)
        {
            if (null != name)
            {
                if (((int)Interop.Constants.MaxPath) < name.Length)
                {
                    throw new ArgumentException(SR.Format(SR.Argument_WaitHandleNameTooLong, name));
                }
            }
            Contract.EndContractBlock();

            uint eventFlags = initialState ? (uint)Interop.Constants.CreateEventInitialSet : 0;

            IntPtr unsafeHandle;
            switch (mode)
            {
                case EventResetMode.ManualReset:
                    eventFlags |= (uint)Interop.Constants.CreateEventManualReset;
                    break;

                case EventResetMode.AutoReset:
                    break;

                default:
                    throw new ArgumentException(SR.Format(SR.Argument_InvalidFlag, name));
            };

            unsafeHandle = Interop.mincore.CreateEventEx(IntPtr.Zero, name, eventFlags, (uint)Interop.Constants.EventAllAccess);
            uint errorCode = Interop.mincore.GetLastError();
            SafeWaitHandle _handle = new SafeWaitHandle(unsafeHandle, true);

            if (_handle.IsInvalid)
            {
                _handle.SetHandleAsInvalid();
                if (null != name && 0 != name.Length && (uint)Interop.Constants.ErrorInvalidHandle == errorCode)
                    throw new WaitHandleCannotBeOpenedException(SR.Format(SR.Threading_WaitHandleCannotBeOpenedException_InvalidHandle, name));

                throw ExceptionFromCreationError(errorCode, name);
            }
            SafeWaitHandle = _handle;
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        public EventWaitHandle(bool initialState, EventResetMode mode, string name, out bool createdNew)
        {
            if (null != name && ((int)Interop.Constants.MaxPath) < name.Length)
            {
                throw new ArgumentException(SR.Format(SR.Argument_WaitHandleNameTooLong, name));
            }
            Contract.EndContractBlock();

            SafeWaitHandle _handle = null;
            uint eventFlags = initialState ? (uint)Interop.Constants.CreateEventInitialSet : 0;
            switch (mode)
            {
                case EventResetMode.ManualReset:
                    eventFlags |= (uint)Interop.Constants.CreateEventManualReset;
                    break;
                case EventResetMode.AutoReset:
                    break;

                default:
                    throw new ArgumentException(SR.Format(SR.Argument_InvalidFlag, name));
            };

            IntPtr unsafeHandle = Interop.mincore.CreateEventEx(IntPtr.Zero, name, eventFlags, (uint)Interop.Constants.EventAllAccess);
            uint errorCode = Interop.mincore.GetLastError();
            _handle = new SafeWaitHandle(unsafeHandle, true);

            if (_handle.IsInvalid)
            {
                _handle.SetHandleAsInvalid();
                if (null != name && 0 != name.Length && (uint)Interop.Constants.ErrorInvalidHandle == errorCode)
                    throw new WaitHandleCannotBeOpenedException(SR.Format(SR.Threading_WaitHandleCannotBeOpenedException_InvalidHandle, name));

                throw ExceptionFromCreationError(errorCode, name);
            }
            createdNew = errorCode != (uint)Interop.Constants.ErrorAlreadyExists;
            SafeWaitHandle = _handle;
        }

        [System.Security.SecurityCritical]  // auto-generated
        private EventWaitHandle(SafeWaitHandle handle)
        {
            SafeWaitHandle = handle;
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        public static EventWaitHandle OpenExisting(string name)
        {
            EventWaitHandle result;
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

        [System.Security.SecurityCritical]  // auto-generated_required
        public static bool TryOpenExisting(string name, out EventWaitHandle result)
        {
            return OpenExistingWorker(name, out result) == OpenExistingResult.Success;
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        private static OpenExistingResult OpenExistingWorker(string name, out EventWaitHandle result)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name", SR.ArgumentNull_WithParamName);
            }

            if (name.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyName, "name");
            }

            if (null != name && ((int)Interop.Constants.MaxPath) < name.Length)
            {
                throw new ArgumentException(SR.Format(SR.Argument_WaitHandleNameTooLong, name));
            }

            Contract.EndContractBlock();

            result = null;

            IntPtr unsafeHandle = Interop.mincore.OpenEvent((uint)(Interop.Constants.EventModifyState | Interop.Constants.Synchronize), false, name);

            uint errorCode = Interop.mincore.GetLastError();
            SafeWaitHandle myHandle = new SafeWaitHandle(unsafeHandle, true);

            if (myHandle.IsInvalid)
            {
                if ((uint)Interop.Constants.ErrorFileNotFound == errorCode || (uint)Interop.Constants.ErrorInvalidName == errorCode)
                    return OpenExistingResult.NameNotFound;
                if ((uint)Interop.Constants.ErrorPathNotFound == errorCode)
                    return OpenExistingResult.PathNotFound;
                if (null != name && 0 != name.Length && (uint)Interop.Constants.ErrorInvalidHandle == errorCode)
                    return OpenExistingResult.NameInvalid;
                //this is for passed through Win32Native Errors
                throw ExceptionFromCreationError(errorCode, name);
            }
            result = new EventWaitHandle(myHandle);
            return OpenExistingResult.Success;
        }
        [System.Security.SecuritySafeCritical]  // auto-generated
        public bool Reset()
        {
            waitHandle.DangerousAddRef();
            try
            {
                bool res = Interop.mincore.ResetEvent(waitHandle.DangerousGetHandle());
                if (!res)
                    throw new IOException(SR.Arg_IOException, (int)Interop.mincore.GetLastError());
                return res;
            }
            finally
            {
                waitHandle.DangerousRelease();
            }
        }
        [System.Security.SecuritySafeCritical]  // auto-generated
        public bool Set()
        {
            waitHandle.DangerousAddRef();
            try
            {
                bool res = Interop.mincore.SetEvent(waitHandle.DangerousGetHandle());

                if (!res)
                    throw new IOException(SR.Arg_IOException, (int)Interop.mincore.GetLastError());

                return res;
            }
            finally
            {
                waitHandle.DangerousRelease();
            }
        }
    }
}

