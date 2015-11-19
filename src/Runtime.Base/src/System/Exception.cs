// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System
{
    public abstract class Exception
    {
        private string _exceptionString;

        internal Exception() { }

        internal Exception(String str)
        {
            _exceptionString = str;
        }
    }

    internal sealed class NullReferenceException : Exception
    {
        public NullReferenceException() { }
    }

    internal sealed class InvalidOperationException : Exception
    {
        public InvalidOperationException() { }
    }

    internal sealed class ArgumentOutOfRangeException : Exception
    {
        public ArgumentOutOfRangeException() { }
    }

    internal sealed class IndexOutOfRangeException : Exception
    {
        public IndexOutOfRangeException() { }
    }

    internal sealed class ArgumentNullException : Exception
    {
        public ArgumentNullException() { }
    }

    internal sealed class NotImplementedException : Exception
    {
        public NotImplementedException() { }
    }

    internal sealed class NotSupportedException : Exception
    {
        public NotSupportedException() { }
    }

    internal sealed class PlatformNotSupportedException : Exception
    {
        public PlatformNotSupportedException() { }
    }

    internal sealed class InvalidCastException : Exception
    {
        public InvalidCastException() { }
    }

    internal sealed class ArrayTypeMismatchException : Exception
    {
        public ArrayTypeMismatchException() { }
    }

    internal sealed class OverflowException : Exception
    {
        public OverflowException() { }
    }

    internal sealed class ArithmeticException : Exception
    {
        public ArithmeticException() { }
    }

    internal sealed class DivideByZeroException : Exception
    {
        public DivideByZeroException() { }
    }

    internal class OutOfMemoryException : Exception
    {
        public OutOfMemoryException() { }
    }
}
