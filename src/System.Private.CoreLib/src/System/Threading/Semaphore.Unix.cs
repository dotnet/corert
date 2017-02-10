// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;

namespace System.Threading
{
    public sealed partial class Semaphore
    {
        private static void VerifyNameForCreate(string name)
        {
            if (name != null)
            {
                throw new PlatformNotSupportedException(SR.PlatformNotSupported_NamedSynchronizationPrimitives);
            }
        }

        private void CreateSemaphoreCore(int initialCount, int maximumCount, string name, out bool createdNew)
        {
            Debug.Assert(name == null);

            SafeWaitHandle = WaitSubsystem.NewSemaphore(initialCount, maximumCount);
            createdNew = true;
        }

        private static OpenExistingResult OpenExistingWorker(string name, out Semaphore result)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_NamedSynchronizationPrimitives);
        }

        private static int ReleaseCore(IntPtr handle, int releaseCount)
        {
            return WaitSubsystem.ReleaseSemaphore(handle, releaseCount);
        }
    }
}
