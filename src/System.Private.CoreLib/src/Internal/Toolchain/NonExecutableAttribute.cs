// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// 

// 

using System;

namespace Internal.Toolchain
{
    //
    // This attribute is used by the IL2IL toolchain to mark that some
    // method/type shouldn't actually be compiled into CTL/MDIL. It simply exists to pass
    // data around.
    //
    [System.Runtime.CompilerServices.DependencyReductionRoot]
    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    public sealed class NonExecutableAttribute : Attribute
    {
        public NonExecutableAttribute()
        {
        }
    }
}
