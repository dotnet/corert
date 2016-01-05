// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        public sealed override Object Invoke(Object thisObject, Object[] arguments)
        {
            return RuntimeAugments.CallDynamicInvokeMethod(
                thisObject, MethodInvokeInfo.LdFtnResult, null, MethodInvokeInfo.DynamicInvokeMethod, MethodInvokeInfo.DynamicInvokeGenericDictionary, MethodInvokeInfo.DefaultValueString, arguments,
                invokeMethodHelperIsThisCall: false, methodToCallIsThisCall: false);
        }
    }
}

