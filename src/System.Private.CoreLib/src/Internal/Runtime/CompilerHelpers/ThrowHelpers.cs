﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

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

        public static void ThrowNotSupportedException()
        {
            throw new NotSupportedException();
        }

        public static void ThrowBadImageFormatException(ExceptionStringID id)
        {
            throw TypeLoaderExceptionHelper.CreateBadImageFormatException(id);
        }

        public static void ThrowTypeLoadException(ExceptionStringID id, string className, string typeName)
        {
            throw TypeLoaderExceptionHelper.CreateTypeLoadException(id, className, typeName);
        }

        public static void ThrowTypeLoadExceptionWithArgument(ExceptionStringID id, string className, string typeName, string messageArg)
        {
            throw TypeLoaderExceptionHelper.CreateTypeLoadException(id, className, typeName, messageArg);
        }

        public static void ThrowMissingMethodException(ExceptionStringID id, string methodName)
        {
            throw TypeLoaderExceptionHelper.CreateMissingMethodException(id, methodName);
        }

        public static void ThrowMissingFieldException(ExceptionStringID id, string fieldName)
        {
            throw TypeLoaderExceptionHelper.CreateMissingFieldException(id, fieldName);
        }

        public static void ThrowFileNotFoundException(ExceptionStringID id, string fileName)
        {
            throw TypeLoaderExceptionHelper.CreateFileNotFoundException(id, fileName);
        }

        public static void ThrowInvalidProgramException(ExceptionStringID id, string methodName)
        {
            throw TypeLoaderExceptionHelper.CreateInvalidProgramException(id, methodName);
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
