// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Diagnostics
{
    public static class ExceptionExtensions
    {
        public static IntPtr[] GetStackIPs(this Exception e)
        {
            return e.GetStackIPs();
        }
    }
}
