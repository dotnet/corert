// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// These methods are targeted by the ArrayMethodILEmitter in the compiler.
    /// </summary>
    internal static class ArrayMethodILHelpers
    {
        private static void ThrowIndexOutOfRangeException()
        {
            throw new IndexOutOfRangeException();
        }
    }
}
