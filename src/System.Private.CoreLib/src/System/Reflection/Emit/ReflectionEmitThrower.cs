// Licensed to the.NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Reflection.Emit
{
    internal static class ReflectionEmitThrower
    {
        public static void ThrowPlatformNotSupportedException()
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ReflectionEmit);
        }
    }
}
