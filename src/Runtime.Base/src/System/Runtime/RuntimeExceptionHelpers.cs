// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime;

namespace System
{
    public class RuntimeExceptionHelpers
    {
        public static void FailFast(String message)
        {
            InternalCalls.RhpFallbackFailFast();
        }
    }
}