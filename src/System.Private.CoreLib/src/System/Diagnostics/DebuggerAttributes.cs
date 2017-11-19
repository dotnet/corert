// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: Attributes for debugger
**
**
===========================================================*/

using System;
using System.Runtime.InteropServices;

namespace System.Diagnostics
{
    //
    // This attribute is used by the IL2IL toolchain to mark generated code to control debugger stepping policy
    //
    [System.Runtime.CompilerServices.DependencyReductionRoot]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false)]
    public sealed class DebuggerStepThroughAttribute : Attribute
    {
        public DebuggerStepThroughAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false)]
    public sealed class DebuggerGuidedStepThroughAttribute : Attribute
    {
        public DebuggerGuidedStepThroughAttribute() { }
    }
}


