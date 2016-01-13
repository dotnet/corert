// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// These methods are used to throw exceptions from generated code.
    /// </summary>
    internal static class ThrowHelpers
    {
        private static void ThrowOverflowException()
        {
            throw new IndexOutOfRangeException();
        }

        private static void ThrowIndexOutOfRangeException()
        {
            throw new IndexOutOfRangeException();
        }

        private static void ThrowNullReferenceException()
        {
            throw new IndexOutOfRangeException();
        }

        private static void ThrowDivideByZeroException()
        {
            throw new IndexOutOfRangeException();
        }
    }
}
