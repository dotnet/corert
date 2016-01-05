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

using global::Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Execution.MethodInvokers
{
    internal abstract class MethodInvokerWithMethodInvokeInfo : MethodInvoker
    {
        public MethodInvokerWithMethodInvokeInfo(MethodInvokeInfo methodInvokeInfo)
        {
            MethodInvokeInfo = methodInvokeInfo;
        }

        public override Delegate CreateDelegate(RuntimeTypeHandle delegateType, Object target, bool isStatic, bool isVirtual, bool isOpen)
        {
            return RuntimeAugments.CreateDelegate(
                delegateType,
                MethodInvokeInfo.LdFtnResult,
                target,
                isStatic: isStatic,
                isOpen: isOpen);
        }

        //
        // Creates the appropriate flavor of Invoker depending on the calling convention "shape" (static, instance or virtual.)
        //
        internal static MethodInvoker CreateMethodInvoker(MetadataReader reader, RuntimeTypeHandle declaringTypeHandle, MethodHandle methodHandle, MethodInvokeInfo methodInvokeInfo)
        {
            Method method = methodHandle.GetMethod(reader);
            MethodAttributes methodAttributes = method.Flags;
            if (0 != (methodAttributes & MethodAttributes.Static))
                return new StaticMethodInvoker(methodInvokeInfo);
            else if (methodInvokeInfo.VirtualResolveData != IntPtr.Zero)
                return new VirtualMethodInvoker(methodInvokeInfo, declaringTypeHandle);
            else
                return new InstanceMethodInvoker(methodInvokeInfo, declaringTypeHandle);
        }

        protected MethodInvokeInfo MethodInvokeInfo { get; private set; }
    }
}

