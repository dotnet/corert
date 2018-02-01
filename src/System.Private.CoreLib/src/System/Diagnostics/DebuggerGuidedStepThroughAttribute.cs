// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Diagnostics
{
    [System.Runtime.CompilerServices.ReflectionBlocked]
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false)]
    public sealed class DebuggerGuidedStepThroughAttribute : Attribute
    {
        public DebuggerGuidedStepThroughAttribute() { }
    }
}
