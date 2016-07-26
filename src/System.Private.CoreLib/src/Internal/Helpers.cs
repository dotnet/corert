// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Threading
{
    /// <summary>
    /// Contains .NET Native-specific Sleep and Spin-wait logic
    /// </summary>
    internal static class Helpers
    {
        private static readonly System.Threading.WaitHandle s_sleepHandle = new System.Threading.ManualResetEvent(false);

        static internal void Sleep(uint milliseconds)
        {
            if (milliseconds == 0)
            {
                System.Threading.SpinWait.Yield();
            }
            else
            {
                s_sleepHandle.WaitOne((int)milliseconds);
            }
        }

        internal static void Spin(int iterations)
        {
            System.Threading.SpinWait.Spin(iterations); // Wait a few dozen instructions to let another processor release lock. 
        }
    }
}
