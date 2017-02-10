// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;
using Microsoft.Win32.SafeHandles;

namespace System.Threading
{
    public abstract partial class WaitHandle
    {
        private static bool WaitOneCore(IntPtr handle, int millisecondsTimeout) =>
            WaitSubsystem.Wait(handle, millisecondsTimeout);

        private static int WaitAnyCore(
            RuntimeThread currentThread,
            SafeWaitHandle[] safeWaitHandles,
            WaitHandle[] waitHandles,
            int millisecondsTimeout)
        {
            return WaitSubsystem.Wait(currentThread, safeWaitHandles, waitHandles, false, millisecondsTimeout);
        }

        private static bool WaitAllCore(
            RuntimeThread currentThread,
            SafeWaitHandle[] safeWaitHandles,
            WaitHandle[] waitHandles,
            int millisecondsTimeout)
        {
            return WaitSubsystem.Wait(currentThread, safeWaitHandles, waitHandles, true, millisecondsTimeout) != WaitTimeout;
        }

        private static bool SignalAndWaitCore(IntPtr handleToSignal, IntPtr handleToWaitOn, int millisecondsTimeout)
        {
            return WaitSubsystem.SignalAndWait(handleToSignal, handleToWaitOn, millisecondsTimeout);
        }
    }
}
