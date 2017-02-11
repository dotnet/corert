// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Threading
{
    public partial class EventWaitHandle
    {
        private static void VerifyNameForCreate(string name)
        {
            if (name != null)
            {
                throw new PlatformNotSupportedException(SR.PlatformNotSupported_NamedSynchronizationPrimitives);
            }
        }

        private void CreateEventCore(bool initialState, EventResetMode mode, string name, out bool createdNew)
        {
            Debug.Assert(name == null);

            SafeWaitHandle = WaitSubsystem.NewEvent(initialState, mode);
            createdNew = true;
        }

        private static OpenExistingResult OpenExistingWorker(string name, out EventWaitHandle result)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_NamedSynchronizationPrimitives);
        }

        private static bool ResetCore(IntPtr handle)
        {
            WaitSubsystem.ResetEvent(handle);
            return true;
        }

        private static bool SetCore(IntPtr handle)
        {
            WaitSubsystem.SetEvent(handle);
            return true;
        }
    }
}
