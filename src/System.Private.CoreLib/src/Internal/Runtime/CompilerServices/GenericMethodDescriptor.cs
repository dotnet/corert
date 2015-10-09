// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.Runtime.CompilerServices
{
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
