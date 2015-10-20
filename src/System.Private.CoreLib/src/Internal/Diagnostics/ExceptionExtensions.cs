// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
