// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace System.Threading
{
    //
    // Unix-specific implementation of Timer
    //
    internal partial class TimerQueue
    {
        private void SetTimer(uint actualDuration)
        {
            // UNIXTODO: Timer
            throw new NotImplementedException();
        }

        private void ReleaseTimer()
        {
        }

        private static int TickCount
        {
            get
            {
                return Environment.TickCount;
            }
        }
    }

    internal sealed partial class TimerQueueTimer
    {
        private void SignalNoCallbacksRunning()
        {
            SafeWaitHandle waitHandle = _notifyWhenNoCallbacksRunning.SafeWaitHandle;

            waitHandle.DangerousAddRef();
            try
            {
                WaitSubsystem.SetEvent(waitHandle.DangerousGetHandle());
            }
            finally
            {
                waitHandle.DangerousRelease();
            }
        }
    }
}
