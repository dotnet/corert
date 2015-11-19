// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class NativeCallableAttribute : Attribute
    {
#pragma warning disable 649 // field never assigned to
        // Optional. If omitted, then the method is native callable, but no EAT is emitted.
        public string EntryPoint;

        // Optional. If omitted a default will be chosen by the compiler.
        public CallingConvention CallingConvention;

        public NativeCallableAttribute()
        {
        }
    }
}
