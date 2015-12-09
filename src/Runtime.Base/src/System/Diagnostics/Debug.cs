// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime;
using System.Runtime.CompilerServices;

namespace System.Diagnostics
{
    internal static class Debug
    {
        [System.Diagnostics.Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                EH.FallbackFailFast(RhFailFastReason.InternalError, null);
            }
        }
    }
}
