// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.IO;

namespace System.Threading
{
    public sealed partial class Mutex
    {
        private void CreateMutexCore(bool initiallyOwned, string name, out bool createdNew)
        {
            if (name != null)
            {
                throw new PlatformNotSupportedException(SR.PlatformNotSupported_NamedSynchronizationPrimitives);
            }

            SafeWaitHandle = WaitSubsystem.NewMutex(initiallyOwned);
            createdNew = true;
        }

        private static OpenExistingResult OpenExistingWorker(string name, out Mutex result)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_NamedSynchronizationPrimitives);
        }

        private static void ReleaseMutexCore(SafeWaitHandle handle)
        {
            WaitSubsystem.ReleaseMutex(handle.DangerousGetHandle());
        }
    }
}
