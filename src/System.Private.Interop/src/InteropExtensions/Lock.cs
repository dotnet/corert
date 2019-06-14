// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Threading
{
    /// <summary>
    /// Simple wrapper around Monitor.Enter and Exit exposing interface as expected by 
    /// System.Private.InteropServices.__ComObject
    /// </summary>
    public class Lock
    {
        private object _lock = new object();
        public void Acquire()
        {
            Monitor.Enter(_lock);
        }
        public void Release()
        {
            Monitor.Exit(_lock);
        }
    }
}
