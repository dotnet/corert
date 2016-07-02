// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//    Internal.Reflection.Execution
//    -------------------------------------------------
//      Why does this exist?:
//        Unlike the desktop, RH uses Internal.Reflection.Core for
//        "classic reflection" emulation as well as LMR, using
//        the Internal.Reflection.Core.Execution contract.
//
//        Internal.Reflection.Core.Execution has an abstract model
//        for an "execution engine" - this contract provides the
//        concrete implementation of this model for Redhawk.
//
//
//      Implemented by:
//        Reflection.Execution.dll on RH
//        N/A on desktop:
//
//      Consumed by:
//        Redhawk app's directly via an under-the-hood ILTransform.
//        System.Private.CoreLib.dll, via a callback (see Internal.System.Runtime.Augment)
//

using global::System;
using global::System.Collections.Generic;
using global::System.Reflection;
using global::System.Runtime.CompilerServices;

using global::Internal.Runtime.Augments;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;

namespace Internal.Reflection.Execution
{
    [EagerOrderedStaticConstructor(EagerStaticConstructorOrder.ReflectionExecution)]
    public static class ReflectionExecution
    {
        /// <summary>
        /// This eager constructor initializes runtime reflection support. As part of ExecutionEnvironmentImplementation
        /// initialization it enumerates the modules and registers the ones containing EmbeddedMetadata reflection blobs
        /// in its _moduleToMetadataReader map.
        /// </summary>
        static ReflectionExecution()
        {
            // Initialize Reflection.Core's one and only ExecutionDomain.
            ExecutionEnvironmentImplementation executionEnvironment = new ExecutionEnvironmentImplementation();
            ReflectionDomainSetupImplementation setup = new ReflectionDomainSetupImplementation(executionEnvironment);
            ReflectionCoreExecution.InitializeExecutionDomain(setup, executionEnvironment);

            // Initialize our two communication with System.Private.CoreLib.
            ExecutionDomain executionDomain = ReflectionCoreExecution.ExecutionDomain;
            ReflectionExecutionDomainCallbacksImplementation runtimeCallbacks = new ReflectionExecutionDomainCallbacksImplementation(executionDomain, executionEnvironment);
            RuntimeAugments.Initialize(runtimeCallbacks);

#if ENABLE_REFLECTION_TRACE
            ReflectionTracingInitializer.Initialize();
#endif

            DefaultAssemblyNamesForGetType =
                new String[]
                {
                    DefaultAssemblyNameForGetType,
                };

            ExecutionEnvironment = executionEnvironment;
            setup.InstallModuleRegistrationCallbacks();
        }

        //
        // This entry is targeted by the ILTransformer to implement Type.GetType()'s ability to detect the calling assembly and use it as
        // a default assembly name.
        //
        public static Type GetType(String typeName, String callingAssemblyName, bool throwOnError, bool ignoreCase)
        {
            LowLevelListWithIList<String> defaultAssemblies = new LowLevelListWithIList<String>();
            defaultAssemblies.Add(callingAssemblyName);
            defaultAssemblies.AddRange(DefaultAssemblyNamesForGetType);
            return ReflectionCoreExecution.ExecutionDomain.GetType(typeName, throwOnError, ignoreCase, defaultAssemblies);
        }

        internal static ExecutionEnvironmentImplementation ExecutionEnvironment { get; private set; }

        //@todo: Is there a better way than hard-coding?
        internal const String DefaultAssemblyNameForGetType = "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
        internal static IEnumerable<String> DefaultAssemblyNamesForGetType;
    }
}

