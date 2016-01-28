// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
