// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Threading;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;

using global::Internal.Runtime.Augments;
using global::Internal.Reflection.Execution;
using global::Internal.Reflection.Core.Execution;

namespace Internal.Reflection.Execution.MethodInvokers
{
    //
    // Implements Invoke() for static methods.
    //
    internal sealed class StaticMethodInvoker : MethodInvokerWithMethodInvokeInfo
    {
        public StaticMethodInvoker(MethodInvokeInfo methodInvokeInfo)
            : base(methodInvokeInfo)
        {
        }

        [DebuggerGuidedStepThroughAttribute]
        public sealed override Object Invoke(Object thisObject, Object[] arguments)
        {
            object result = RuntimeAugments.CallDynamicInvokeMethod(
                thisObject, MethodInvokeInfo.LdFtnResult, null, MethodInvokeInfo.DynamicInvokeMethod, MethodInvokeInfo.DynamicInvokeGenericDictionary, MethodInvokeInfo.DefaultValueString, arguments,
                invokeMethodHelperIsThisCall: false, methodToCallIsThisCall: false);
            System.Diagnostics.DebugAnnotations.PreviousCallContainsDebuggerStepInCode();
            return result;
        }
    }
}

