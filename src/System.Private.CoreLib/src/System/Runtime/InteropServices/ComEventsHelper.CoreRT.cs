// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.InteropServices
{
    public static class ComEventsHelper
    {
        public static void Combine(object rcw, Guid iid, int dispid, Delegate d)
        {
            throw new PlatformNotSupportedException();
        }

        public static Delegate? Remove(object rcw, Guid iid, int dispid, Delegate d)
        {
            throw new PlatformNotSupportedException();
        }
    }
}
