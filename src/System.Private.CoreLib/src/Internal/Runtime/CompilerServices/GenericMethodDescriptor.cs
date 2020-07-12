// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Internal.Runtime.CompilerServices
{
    [System.Runtime.CompilerServices.ReflectionBlocked]
    public unsafe struct GenericMethodDescriptor
    {
        internal IntPtr _MethodFunctionPointer;
        internal IntPtr* _MethodDictionaryPointerPointer;

        public IntPtr MethodFunctionPointer
        {
            get
            {
                return _MethodFunctionPointer;
            }
        }
        public IntPtr InstantiationArgument
        {
            get
            {
                return *_MethodDictionaryPointerPointer;
            }
        }
    }
}
