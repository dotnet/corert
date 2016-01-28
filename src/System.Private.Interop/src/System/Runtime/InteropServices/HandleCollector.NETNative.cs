// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace System.Runtime.InteropServices
{
    // .NET Native-specific HandleCollector implementation
    public sealed partial class HandleCollector
    {
        private void Sleep(int milliseconds)
        {
            Interop.MinCore.Sleep((uint)milliseconds);
        }
    }
}
