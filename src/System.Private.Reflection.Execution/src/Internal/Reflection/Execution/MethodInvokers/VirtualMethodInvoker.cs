// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Threading;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;

using global::Internal.Runtime.Augments;
using global::Internal.Runtime.CompilerServices;
using global::Internal.Reflection.Execution;
using global::Internal.Reflection.Core.Execution;

using TargetException = System.ArgumentException;

namespace Internal.Reflection.Execution.MethodInvokers
{
    //
    // Implements Invoke() for virtual methods on interfaces.
    //
    internal sealed class VirtualMethodInvoker : MethodInvokerWithMethodInvokeInfo
    {
        public VirtualMethodInvoker(MethodInvokeInfo methodInvokeInfo, RuntimeTypeHandle declaringTypeHandle)
            : base(methodInvokeInfo)
        {
            _declaringTypeHandle = declaringTypeHandle;
        }

        public sealed override Delegate CreateDelegate(RuntimeTypeHandle delegateType, Object target, bool isStatic, bool isVirtual, bool isOpen)
        {
            if (!isOpen)
            {
                // We're creating a delegate to a virtual override of this method, so resolve the virtual now.
                IntPtr resolvedVirtual = OpenMethodResolver.ResolveMethod(MethodInvokeInfo.VirtualResolveData, target);
                return RuntimeAugments.CreateDelegate(
                                delegateType,
                                resolvedVirtual,
                                target,
                                isStatic: false,
                                isOpen: isOpen);
            }
            else
            {
                // Create an open virtual method by providing the virtual resolver to the delegate type.
                return RuntimeAugments.CreateDelegate(
                    delegateType,
                    MethodInvokeInfo.VirtualResolveData,
                    target,
                    isStatic: false,
                    isOpen: isOpen);
            }
        }

        public sealed override Object Invoke(Object thisObject, Object[] arguments)
        {
            MethodInvokerUtils.ValidateThis(thisObject, _declaringTypeHandle);

            IntPtr resolvedVirtual = OpenMethodResolver.ResolveMethod(MethodInvokeInfo.VirtualResolveData, thisObject);

            Object result = RuntimeAugments.CallDynamicInvokeMethod(
                thisObject, resolvedVirtual, null, MethodInvokeInfo.DynamicInvokeMethod, MethodInvokeInfo.DynamicInvokeGenericDictionary, MethodInvokeInfo.DefaultValueString, arguments,
                invokeMethodHelperIsThisCall: false, methodToCallIsThisCall: true);

            return result;
        }

        private RuntimeTypeHandle _declaringTypeHandle;
    }
}

