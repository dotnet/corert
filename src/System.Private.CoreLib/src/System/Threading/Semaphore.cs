// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Runtime.Versioning;

namespace System.Threading
{
    public sealed partial class Semaphore : WaitHandle
    {
        private const int MAX_PATH = (int)Interop.Constants.MaxPath;

        // creates a nameless semaphore object
        // Win32 only takes maximum count of Int32.MaxValue
        public Semaphore(int initialCount, int maximumCount)
        {
            VerifyCounts(initialCount, maximumCount);

            bool createdNew;
            CreateSemaphoreCore(initialCount, maximumCount, null, out createdNew);
        }

        public Semaphore(int initialCount, int maximumCount, string name)
        {
            VerifyCounts(initialCount, maximumCount);
            VerifyNameForCreate(name);

            bool createdNew;
            CreateSemaphoreCore(initialCount, maximumCount, name, out createdNew);
        }

        public Semaphore(int initialCount, int maximumCount, string name, out bool createdNew)
        {
            VerifyCounts(initialCount, maximumCount);
            VerifyNameForCreate(name);

            CreateSemaphoreCore(initialCount, maximumCount, name, out createdNew);
        }

        private static void VerifyCounts(int initialCount, int maximumCount)
        {
            if (initialCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCount), SR.ArgumentOutOfRange_NeedNonNegNumRequired);
            }

            if (maximumCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCount), SR.ArgumentOutOfRange_NeedNonNegNumRequired);
            }

            if (initialCount > maximumCount)
            {
                throw new ArgumentException(SR.Argument_SemaphoreInitialMaximum);
            }
        }

        public static Semaphore OpenExisting(string name)
        {
            Semaphore result;
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

        public static bool TryOpenExisting(string name, out Semaphore result) =>
            OpenExistingWorker(name, out result) == OpenExistingResult.Success;

        // increase the count on a semaphore, returns previous count
        public int Release() => ReleaseCore(1);

        // increase the count on a semaphore, returns previous count
        public int Release(int releaseCount)
        {
            if (releaseCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(releaseCount), SR.ArgumentOutOfRange_NeedNonNegNumRequired);
            }

            return ReleaseCore(releaseCount);
        }

        private int ReleaseCore(int releaseCount)
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
                return ReleaseCore(waitHandle.DangerousGetHandle(), releaseCount);
            }
            finally
            {
                waitHandle.DangerousRelease();
            }
        }
    }
}

