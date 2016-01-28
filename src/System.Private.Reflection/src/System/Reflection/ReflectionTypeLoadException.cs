// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  ReflectionTypeLoadException
**
==============================================================*/

using global::System;

namespace System.Reflection
{
    public sealed class ReflectionTypeLoadException : Exception
    {
        private Type[] _classes;
        private Exception[] _exceptions;

        public ReflectionTypeLoadException(Type[] classes, Exception[] exceptions) : base(null)
        {
            _classes = classes;
            _exceptions = exceptions;
            HResult = __HResults.COR_E_REFLECTIONTYPELOAD;
        }

        public ReflectionTypeLoadException(Type[] classes, Exception[] exceptions, String message) : base(message)
        {
            _classes = classes;
            _exceptions = exceptions;
            HResult = __HResults.COR_E_REFLECTIONTYPELOAD;
        }

        public Type[] Types
        {
            get { return _classes; }
        }

        public Exception[] LoaderExceptions
        {
            get { return _exceptions; }
        }
    }
}

