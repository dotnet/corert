// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    }
}
