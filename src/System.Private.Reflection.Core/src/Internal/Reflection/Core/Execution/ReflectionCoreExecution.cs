// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//  Internal.Reflection.Core.Execution
//  -------------------------------------------------
//  Why does this exist?:
//   This contract augments Internal.Reflection.Core and adds the ability
//   to create a single "execution domain" which allows creation of
//   a Win8P-style "invokable" Reflection object tree. Reflection objects
//   in this domain unify with the underlying execution engine's
//   native type artifacts (i.e. typeof() and Object.GetType() returns
//   types in this domain.)
//
//  Implemented by:
//     Reflection.Core.dll on RH and desktop.
//
//   Consumed by:
//     RH for "classic reflection".
//     Not used on desktop.

using global::System;
using global::System.Reflection;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.Types;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.MethodInfos;

using global::Internal.LowLevelLinq;
using global::Internal.Metadata.NativeFormat;
using global::Internal.Reflection.Augments;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;

namespace Internal.Reflection.Core.Execution
{
    public static class ReflectionCoreExecution
    {
        //
        // One time initialization to supply the information needed to initialize the default domain and the  
        // the execution environment.
        //
        // Remarks:
        //   This design intentionally restricts you to one ExecutionEnvironment per side-by-side runtime in a process.
        //   Aside from the dubious value of allowing multiple executionEnvironments, there is no good way to
        //   for this scenario:
        //
        //            typeof(Foo).GetTypeInfo()
        //
        //   to "lookup" a domain in order to map the RuntimeTypeHandle to a specific ExecutionEnvironment.
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

        //
        // The ExecutionDomain is the domain that hosts reflection entities created by these constructs:
        //
        //         Type.GetTypeFromHandle()   ( i.e. typeof() )
        //         Object.GetType()
        //         Type.GetType(String name, [bool throwOnError])
        //         Assembly.Load(AssemblyName)
        //      
        // There is only one ExecutionDomain and it is initialized by the Initialize() method. It is the only domain capable
        // of supporting invocation.
        //
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
