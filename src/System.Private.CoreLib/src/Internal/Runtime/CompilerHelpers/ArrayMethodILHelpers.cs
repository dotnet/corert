// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// These methods are targeted by the ArrayMethodILEmitter in the compiler.
    /// </summary>
    static class ArrayMethodILHelpers
    {
        static void ThrowIndexOutOfRangeException()
        {
            throw new IndexOutOfRangeException();
        }
    }
}
