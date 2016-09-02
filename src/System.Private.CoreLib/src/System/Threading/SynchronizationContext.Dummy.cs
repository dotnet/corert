// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Threading
{
    //
    // Dummy implementation of SynchronizationContext
    //
    public partial class SynchronizationContext
    {
        // Get the current SynchronizationContext on the current thread
        public static SynchronizationContext Current
        {
            get
            {
                return s_current;
            }
        }
    }
}
