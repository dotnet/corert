// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Diagnostics;

namespace System.Threading
{
    public partial class EventWaitHandle : WaitHandle
    {
        public EventWaitHandle(bool initialState, EventResetMode mode)
        {
            VerifyMode(mode);

            bool createdNew;
            CreateEventCore(initialState, mode, null, out createdNew);
        }

        public EventWaitHandle(bool initialState, EventResetMode mode, string name)
        {
            VerifyNameForCreate(name);
            VerifyMode(mode);

            bool createdNew;
            CreateEventCore(initialState, mode, name, out createdNew);
        }

        public EventWaitHandle(bool initialState, EventResetMode mode, string name, out bool createdNew)
        {
            VerifyNameForCreate(name);
            VerifyMode(mode);

            CreateEventCore(initialState, mode, name, out createdNew);
        }

        private static void VerifyMode(EventResetMode mode)
        {
            if (mode != EventResetMode.AutoReset && mode != EventResetMode.ManualReset)
            {
                throw new ArgumentException(SR.Argument_InvalidFlag, nameof(mode));
            }
        }

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

        public static bool TryOpenExisting(string name, out EventWaitHandle result)
        {
            return OpenExistingWorker(name, out result) == OpenExistingResult.Success;
        }

        public bool Reset()
        {
            // The field value is modifiable via <see cref="SafeWaitHandle"/>, save it locally to ensure that ref modification
            // is done on the same instance
            SafeWaitHandle waitHandle = _waitHandle;
            if (waitHandle == null)
            {
                ThrowInvalidHandleException();
            }

            waitHandle.DangerousAddRef();
            try
            {
                return ResetCore(_waitHandle.DangerousGetHandle());
            }
            finally
            {
                waitHandle.DangerousRelease();
            }
        }

        public bool Set()
        {
            // The field value is modifiable via the public <see cref="WaitHandle.SafeWaitHandle"/> property, save it locally
            // to ensure that one instance is used in all places in this method
            SafeWaitHandle waitHandle = _waitHandle;
            if (waitHandle == null)
            {
                ThrowInvalidHandleException();
            }

            waitHandle.DangerousAddRef();
            try
            {
                return SetCore(waitHandle.DangerousGetHandle());
            }
            finally
            {
                waitHandle.DangerousRelease();
            }
        }
        
        internal static bool Set(IntPtr handle)
        {
            return SetCore(handle);
        }
    }
}
