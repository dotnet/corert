// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;

namespace Internal.Reflection.Core.Execution
{
    //
    // This class polymorphically implements the MethodBase.Invoke() api and its close cousins. MethodInvokers are designed to be built once and cached
    // for maximum Invoke() throughput.
    //
    public abstract class MethodInvoker
    {
        protected MethodInvoker() { }

        public abstract Object Invoke(Object thisObject, Object[] arguments);
        public abstract Delegate CreateDelegate(RuntimeTypeHandle delegateType, Object target, bool isStatic, bool isVirtual, bool isOpen);
    }
}

