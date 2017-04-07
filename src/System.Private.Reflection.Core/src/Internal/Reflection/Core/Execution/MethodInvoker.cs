// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Reflection.Runtime.General;

namespace Internal.Reflection.Core.Execution
{
    //
    // This class polymorphically implements the MethodBase.Invoke() api and its close cousins. MethodInvokers are designed to be built once and cached
    // for maximum Invoke() throughput.
    //
    public abstract class MethodInvoker
    {
        protected MethodInvoker() { }

        [DebuggerGuidedStepThrough]
        public Object Invoke(Object thisObject, Object[] arguments, Binder binder, BindingFlags invokeAttr, CultureInfo cultureInfo)
        {
            BinderBundle binderBundle = binder.ToBinderBundle(invokeAttr, cultureInfo);
            Object result = Invoke(thisObject, arguments, binderBundle);
            System.Diagnostics.DebugAnnotations.PreviousCallContainsDebuggerStepInCode();
            return result;
        }
        public abstract Object Invoke(Object thisObject, Object[] arguments, BinderBundle binderBundle);
        public abstract Delegate CreateDelegate(RuntimeTypeHandle delegateType, Object target, bool isStatic, bool isVirtual, bool isOpen);

        // This property is used to retrieve the target method pointer. It is used by the RuntimeMethodHandle.GetFunctionPointer API
        public abstract IntPtr LdFtnResult { get; }
    }
}

