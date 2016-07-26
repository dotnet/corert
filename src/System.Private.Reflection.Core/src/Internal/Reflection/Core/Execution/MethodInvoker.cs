// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;

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

