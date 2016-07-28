// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.CompilerServices
{
    // When applied to a type this custom attribute will cause it's cctor to be executed during startup 
    // rather being deferred.'order' define the order of execution relative to other cctor's marked with the same attribute.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    sealed public class EagerOrderedStaticConstructorAttribute : Attribute
    {
        private EagerStaticConstructorOrder _order;
        public EagerOrderedStaticConstructorAttribute(EagerStaticConstructorOrder order)
        {
            _order = order;
        }
        public EagerStaticConstructorOrder Order { get { return _order; } }
    }

    // Defines all the types which require eager cctor execution ,defined order is the order of execution.The enum is
    // grouped by Modules and then by types.

    public enum EagerStaticConstructorOrder : int
    {
        // System.Private.CoreLib
        SystemString,
        SystemPreallocatedOutOfMemoryException,
        SystemEnvironment, // ClassConstructorRunner.Cctor.GetCctor use Lock which inturn use current threadID , so System.Environment
                           // should come before CompilerServicesClassConstructorRunnerCctor
        CompilerServicesClassConstructorRunnerCctor,
        CompilerServicesClassConstructorRunner,

        // System.Private.TypeLoader  
        RuntimeTypeHandleEqualityComparer,
        TypeLoaderEnvironment,
        SystemRuntimeTypeLoaderExports,

        // Interop
        InteropHeap,
        VtableIUnknown,
        McgModuleManager,

        // Per Module Interop
        McgCurrentModule,

        // System.Private.Threading
        Threading,

        // System.Private.Reflection.Execution
        ReflectionExecution,
    }
}
