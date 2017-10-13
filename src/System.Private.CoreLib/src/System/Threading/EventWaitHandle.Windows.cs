// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace System.Threading
{
    public partial class EventWaitHandle
    {
        private const uint AccessRights = (uint)(Interop.Constants.MaximumAllowed | Interop.Constants.Synchronize | Interop.Constants.EventModifyState);

        private EventWaitHandle(SafeWaitHandle handle)
        {
            SafeWaitHandle = handle;
        }

        private static void VerifyNameForCreate(string name)
        {
            if (null != name && ((int)Interop.Constants.MaxPath) < name.Length)
            {
                throw new ArgumentException(SR.Format(SR.Argument_WaitHandleNameTooLong, Interop.Constants.MaxPath), nameof(name));
            }
        }

        private void CreateEventCore(bool initialState, EventResetMode mode, string name, out bool createdNew)
        {
            Debug.Assert((mode == EventResetMode.AutoReset) || (mode == EventResetMode.ManualReset));
            Debug.Assert(name == null || name.Length <= (int)Interop.Constants.MaxPath);

            uint eventFlags = initialState ? (uint)Interop.Constants.CreateEventInitialSet : 0;
            if (mode == EventResetMode.ManualReset)
            {
                eventFlags |= (uint)Interop.Constants.CreateEventManualReset;
            }

            SafeWaitHandle _handle = Interop.mincore.CreateEventEx(IntPtr.Zero, name, eventFlags, AccessRights);

            if (_handle.IsInvalid)
            {
                int errorCode = Marshal.GetLastWin32Error();
                _handle.SetHandleAsInvalid();
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

            SafeWaitHandle = _handle;
        }

        private static OpenExistingResult OpenExistingWorker(string name, out EventWaitHandle result)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name), SR.ArgumentNull_WithParamName);
            }

            if (name.Length == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyName, nameof(name));
            }

            if (null != name && ((int)Interop.Constants.MaxPath) < name.Length)
            {
                throw new ArgumentException(SR.Format(SR.Argument_WaitHandleNameTooLong, Interop.Constants.MaxPath), nameof(name));
            }

            result = null;

            SafeWaitHandle myHandle = Interop.mincore.OpenEvent(AccessRights, false, name);

            if (myHandle.IsInvalid)
            {
                int errorCode = Marshal.GetLastWin32Error();

                if (Interop.Errors.ERROR_FILE_NOT_FOUND == errorCode || Interop.Errors.ERROR_INVALID_NAME == errorCode)
                    return OpenExistingResult.NameNotFound;
                if (Interop.Errors.ERROR_PATH_NOT_FOUND == errorCode)
                    return OpenExistingResult.PathNotFound;
                if (null != name && 0 != name.Length && Interop.Errors.ERROR_INVALID_HANDLE == errorCode)
                    return OpenExistingResult.NameInvalid;

                throw ExceptionFromCreationError(errorCode, name);
            }
            result = new EventWaitHandle(myHandle);
            return OpenExistingResult.Success;
        }

        private static bool ResetCore(IntPtr handle)
        {
            bool res = Interop.mincore.ResetEvent(handle);
            if (!res)
                ThrowSignalOrUnsignalException();
            return res;
        }

        private static bool SetCore(IntPtr handle)
        {
            bool res = Interop.mincore.SetEvent(handle);
            if (!res)
                ThrowSignalOrUnsignalException();
            return res;
        }
    }
}
