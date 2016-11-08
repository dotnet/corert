// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

using Internal.Runtime.Augments;

namespace System.Threading
{
    //
    // WinRT-specific implementation of Timer
    //
    internal partial class TimerQueue
    {
        private Delegate _nativeTimerCallback;
        private Object _nativeTimer;

        private void SetTimer(uint actualDuration)
        {
            if (_nativeTimerCallback == null)
            {
                Debug.Assert(_nativeTimer == null);
                _nativeTimerCallback = WinRTInterop.Callbacks.CreateTimerDelegate(new Action(TimerCallback));
            }

            Object previousNativeTimer = _nativeTimer;
            _nativeTimer = WinRTInterop.Callbacks.CreateTimer(_nativeTimerCallback, TimeSpan.FromMilliseconds(actualDuration));
            if (previousNativeTimer != null)
                WinRTInterop.Callbacks.ReleaseTimer(previousNativeTimer, true);
        }

        private void ReleaseTimer()
        {
            Object previousNativeTimer = _nativeTimer;

            if (previousNativeTimer != null)
            {
                _nativeTimer = null;
                WinRTInterop.Callbacks.ReleaseTimer(previousNativeTimer, false);
            }
        }

        private static void TimerCallback()
        {
            try
            {
                Instance.FireNextTimers();
            }
            catch (Exception ex)
            {
                RuntimeAugments.ReportUnhandledException(ex);
            }
        }
    }
}
