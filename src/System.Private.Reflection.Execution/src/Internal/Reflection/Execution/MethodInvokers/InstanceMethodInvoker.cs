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
using global::Internal.Runtime.CompilerServices;

using TargetException = System.ArgumentException;

namespace Internal.Reflection.Execution.MethodInvokers
{
    //
    // Implements Invoke() for non-virtual instance methods.
    //
    internal sealed class InstanceMethodInvoker : MethodInvokerWithMethodInvokeInfo
    {
        public InstanceMethodInvoker(MethodInvokeInfo methodInvokeInfo, RuntimeTypeHandle declaringTypeHandle)
            : base(methodInvokeInfo)
        {
            _declaringTypeHandle = declaringTypeHandle;
        }

        [DebuggerGuidedStepThroughAttribute]
        public sealed override Object Invoke(Object thisObject, Object[] arguments)
        {
            MethodInvokerUtils.ValidateThis(thisObject, _declaringTypeHandle);
            object result = RuntimeAugments.CallDynamicInvokeMethod(
                thisObject,
                MethodInvokeInfo.LdFtnResult,
                null /*thisPtrDynamicInvokeMethod*/,
                MethodInvokeInfo.DynamicInvokeMethod,
                MethodInvokeInfo.DynamicInvokeGenericDictionary,
                MethodInvokeInfo.MethodInfo,
                arguments,
                invokeMethodHelperIsThisCall: false, 
                methodToCallIsThisCall: true);
            System.Diagnostics.DebugAnnotations.PreviousCallContainsDebuggerStepInCode();
            return result;
        }

        public sealed override Delegate CreateDelegate(RuntimeTypeHandle delegateType, Object target, bool isStatic, bool isVirtual, bool isOpen)
        {
            if (isOpen)
            {
                return RuntimeAugments.CreateDelegate(
                    delegateType,
                    new OpenMethodResolver(_declaringTypeHandle, MethodInvokeInfo.LdFtnResult, 0).ToIntPtr(),
                    target,
                    isStatic: isStatic,
                    isOpen: isOpen);
            }
            else
            {
                return base.CreateDelegate(delegateType, target, isStatic, isVirtual, isOpen);
            }
        }


        private RuntimeTypeHandle _declaringTypeHandle;
    }
}

