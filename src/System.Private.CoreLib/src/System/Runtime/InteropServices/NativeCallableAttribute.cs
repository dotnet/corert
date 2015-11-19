// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    //BARTOK expects
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class NativeCallableAttribute : Attribute
    {
        // Optional. If omitted, then the method is native callable, but no EAT is emitted.
        public string EntryPoint;

        // Optional. If omitted a default will be chosen by the compiler.
        public CallingConvention CallingConvention;

        public NativeCallableAttribute()
        {
        }
    }
}
