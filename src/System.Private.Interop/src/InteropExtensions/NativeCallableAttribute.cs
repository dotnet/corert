// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
