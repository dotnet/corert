// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// These methods are used to throw exceptions from generated code. The type and methods
    /// need to be public as they constitute a public contract with the .NET Native toolchain.
    /// </summary>
    [System.Runtime.CompilerServices.DependencyReductionRoot] /* keep rooted as code gen may add references to these */
    public static class ThrowHelpers
    {
        public static void ThrowOverflowException()
        {
            throw new OverflowException();
        }

        public static void ThrowIndexOutOfRangeException()
        {
            throw new IndexOutOfRangeException();
        }

        public static void ThrowNullReferenceException()
        {
            throw new NullReferenceException();
        }

        public static void ThrowDivideByZeroException()
        {
            throw new DivideByZeroException();
        }

        public static void ThrowArrayTypeMismatchException()
        {
            throw new ArrayTypeMismatchException();
        }

        public static void ThrowPlatformNotSupportedException()
        {
            throw new PlatformNotSupportedException();
        }

        public static void ThrowTypeLoadException()
        {
            throw new TypeLoadException();
        }

        public static void ThrowArgumentException()
        {
            throw new ArgumentException();
        }

        public static void ThrowArgumentOutOfRangeException()
        {
            throw new ArgumentOutOfRangeException();
        }
    }
}
