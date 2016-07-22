// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.General;

using global::Internal.LowLevelLinq;
using global::Internal.Reflection.Augments;
using global::Internal.Reflection.Core.Execution;

namespace Internal.Reflection.Core.Execution
{
    public static class ReflectionCoreExecution
    {
        //
        // One time initialization to supply the information needed to initialize the execution environment.
        //
        public static void InitializeExecutionDomain(ReflectionDomainSetup executionDomainSetup, ExecutionEnvironment executionEnvironment)
        {
            ExecutionDomain executionDomain = new ExecutionDomain(executionDomainSetup, executionEnvironment);
            //@todo: This check has a race window but since this is a private api targeted by the toolchain, perhaps this is not so critical.
            if (_executionDomain != null)
                throw new InvalidOperationException(); // Multiple Initializes not allowed.
            _executionDomain = executionDomain;

            ReflectionCoreCallbacks reflectionCallbacks = new ReflectionCoreCallbacksImplementation();
            ReflectionAugments.Initialize(reflectionCallbacks);
            return;
        }

        public static ExecutionDomain ExecutionDomain
        {
            get
            {
                return _executionDomain;
            }
        }

        internal static ExecutionEnvironment ExecutionEnvironment
        {
            get
            {
                return ExecutionDomain.ExecutionEnvironment;
            }
        }

        private volatile static ExecutionDomain _executionDomain;
    }
}
