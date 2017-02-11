// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;

namespace System.Threading
{
    public sealed partial class Mutex
    {
        private static void VerifyNameForCreate(string name)
        {
            if (name != null)
            {
                throw new PlatformNotSupportedException(SR.PlatformNotSupported_NamedSynchronizationPrimitives);
            }
        }

        private void CreateMutexCore(bool initiallyOwned, string name, out bool createdNew)
        {
            Debug.Assert(name == null);

            SafeWaitHandle = WaitSubsystem.NewMutex(initiallyOwned);
            createdNew = true;
        }

        private static OpenExistingResult OpenExistingWorker(string name, out Mutex result)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_NamedSynchronizationPrimitives);
        }

        private static void ReleaseMutexCore(IntPtr handle)
        {
            WaitSubsystem.ReleaseMutex(handle);
        }
    }
}
