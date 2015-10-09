// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
