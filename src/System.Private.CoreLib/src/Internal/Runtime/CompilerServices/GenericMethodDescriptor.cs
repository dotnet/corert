// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
